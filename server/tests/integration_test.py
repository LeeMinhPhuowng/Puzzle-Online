import base64
import socket
import subprocess
import sys
import tempfile
import threading
import time
import uuid
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
PORT = 17877


def b64(value):
    return base64.urlsafe_b64encode(str(value).encode()).decode().rstrip("=")


def encode(kind, **fields):
    return kind + "".join(f"|{key}={b64(value)}" for key, value in fields.items()) + "\n"


def decode(line):
    parts = line.strip().split("|")
    output = {"type": parts[0]}
    for part in parts[1:]:
        key, value = part.split("=", 1)
        value += "=" * (-len(value) % 4)
        output[key] = base64.urlsafe_b64decode(value).decode()
    return output


class Client:
    def __init__(self):
        self.socket = socket.create_connection(("127.0.0.1", PORT), timeout=4)
        self.file = self.socket.makefile("r", encoding="utf-8", newline="\n")
        self.wait_for("SERVER_HELLO")

    def send(self, kind, **fields):
        self.socket.sendall(encode(kind, **fields).encode())

    def wait_for(self, kind, timeout=5):
        deadline = time.time() + timeout
        while time.time() < deadline:
            self.socket.settimeout(max(.1, deadline - time.time()))
            message = decode(self.file.readline())
            if message["type"] == "ERROR":
                raise AssertionError(f"server error: {message}")
            if message["type"] == kind:
                return message
        raise TimeoutError(kind)

    def close(self):
        self.socket.close()


def main():
    with tempfile.TemporaryDirectory(prefix="puzzle-server-test-") as data_dir:
        command = ["powershell", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", str(ROOT / "run-server.ps1"),
                   "-Port", str(PORT), "-DataDirectory", data_dir]
        process = subprocess.Popen(command, cwd=ROOT, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True)
        lines = []

        def collect():
            for line in process.stdout:
                lines.append(line.rstrip())

        threading.Thread(target=collect, daemon=True).start()
        deadline = time.time() + 15
        while time.time() < deadline:
            try:
                first = Client()
                break
            except OSError:
                if process.poll() is not None:
                    raise RuntimeError("server exited:\n" + "\n".join(lines))
                time.sleep(.2)
        else:
            raise TimeoutError("server did not start:\n" + "\n".join(lines))

        second = Client()
        suffix = uuid.uuid4().hex[:7]
        alice, bob = "alice_" + suffix, "bob_" + suffix
        try:
            first.send("REGISTER", username=alice, password="test1234")
            auth_a = first.wait_for("AUTH_OK")
            assert auth_a["coins"] == "300"
            second.send("REGISTER", username=bob, password="test1234")
            second.wait_for("AUTH_OK")

            first.send("FRIEND_ADD", username=bob)
            first.wait_for("LOBBY_SNAPSHOT")
            first.send("CREATE_ROOM", name="Integration Room", max=2, puzzleId="sunset_3x3")
            room_a = first.wait_for("ROOM_SNAPSHOT")
            room_id = room_a["roomId"]
            second.send("JOIN_ROOM", roomId=room_id)
            second.wait_for("ROOM_SNAPSHOT")

            first.send("READY", ready="true")
            first.wait_for("ROOM_SNAPSHOT")
            second.send("READY", ready="true")
            second.wait_for("ROOM_SNAPSHOT")
            first.send("START_GAME")
            game = first.wait_for("GAME_SNAPSHOT")
            assert game["total"] == "9"

            piece_zero = game["pieces"].split("\x1e")[0].split("\x1f")
            initial_rotation = int(piece_zero[5])
            turns_to_zero = ((360 - initial_rotation) % 360) // 90

            first.send("LOCK_PIECE", pieceId=0)
            first.wait_for("PIECE_LOCKED")
            first.send("MOVE_PIECE", pieceId=0, x=1 / 6, y=1 / 6)
            for _ in range(turns_to_zero):
                first.send("ROTATE_PIECE", pieceId=0, degrees=90)
            first.send("PLACE_PIECE", pieceId=0)
            placed = first.wait_for("GAME_SNAPSHOT")
            for _ in range(12):
                if int(placed.get("placed", "0")) >= 1:
                    break
                placed = first.wait_for("GAME_SNAPSHOT")
            assert int(placed["placed"]) >= 1

            first.send("SAVE_GAME", label="Integration save")
            first.wait_for("SAVE_CREATED")
            first.send("RESTART_GAME")
            restarted = first.wait_for("GAME_SNAPSHOT")
            assert restarted["instanceId"] != game["instanceId"]

            token = auth_a["token"]
            first.close()
            time.sleep(.3)
            resumed = Client()
            resumed.send("RESUME", token=token)
            resume_message = resumed.wait_for("RESUME_OK")
            assert resume_message["username"] == alice
            resumed.wait_for("ROOM_SNAPSHOT")
            resumed.wait_for("GAME_SNAPSHOT")
            resumed.close()
            print("PASS: auth, friends, room, ready, realtime puzzle, save, restart and reconnect")
        finally:
            second.close()
            process.terminate()
            try:
                process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                process.kill()


if __name__ == "__main__":
    try:
        main()
    except Exception as error:
        print("FAIL:", error)
        sys.exit(1)
