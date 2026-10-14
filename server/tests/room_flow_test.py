"""Room and emoji flows with three real TCP clients and a disposable MySQL schema."""
import socket
import subprocess
import tempfile
import threading
import time
import uuid
import re
from pathlib import Path
from integration_test import encode, decode

ROOT = Path(__file__).resolve().parents[1]
JAVA_HOME = re.search(r"java.home = (.+)", subprocess.run(["java", "-XshowSettings:properties", "-version"], capture_output=True, text=True).stderr)[1].strip()
JAVA = str(Path(JAVA_HOME) / "bin/java.exe")
JAVAC = str(Path(JAVA_HOME) / "bin/javac.exe")


class Client:
    def __init__(self, port):
        self.history = []
        self.sock = socket.create_connection(("127.0.0.1", port), timeout=5)
        self.file = self.sock.makefile("r", encoding="utf-8")
        self.wait("SERVER_HELLO")

    def send(self, kind, **fields):
        self.history.append("send " + kind)
        self.sock.sendall(encode(kind, **fields).encode())

    def wait(self, kind, predicate=lambda m: True):
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            self.sock.settimeout(max(.1, deadline - time.monotonic()))
            line = self.file.readline()
            if not line:
                raise AssertionError("server closed connection")
            message = decode(line)
            message["kind"] = line.split("|", 1)[0].strip()
            self.history.append("receive " + message["kind"])
            if message["kind"] == "ERROR" and kind != "ERROR":
                raise AssertionError(message)
            if message["kind"] == kind and predicate(message):
                return message
        raise TimeoutError(kind)

    def close(self):
        self.file.close()
        self.sock.close()


def rows(snapshot, field):
    return [r.split("\x1f") for r in snapshot[field].split("\x1e") if r]


def main():
    schema = "puzzle_codex_test_" + uuid.uuid4().hex[:10]
    with tempfile.TemporaryDirectory(prefix="puzzle-competitive-") as tmp:
        tmp = Path(tmp)
        build = tmp / "classes"
        build.mkdir()
        jars = list((ROOT / "lib").glob("*.jar"))
        cp = ";".join(map(str, jars))
        sources = list((ROOT / "src/main/java").rglob("*.java")) + list((ROOT / "tests").glob("*.java"))
        subprocess.run([JAVAC, "-encoding", "UTF-8", "-cp", cp, "-d", str(build), *map(str, sources)], check=True)
        cp = str(build) + ";" + cp
        fixture = [JAVA, "-cp", cp, "IsolatedTestDatabase"]
        config = ROOT / "db.properties"
        process = None
        clients = []
        try:
            subprocess.run([*fixture, "create", str(config), schema, str(ROOT / "schema.sql"), str(tmp / "db.properties")], check=True)
            with socket.socket() as probe:
                probe.bind(("127.0.0.1", 0))
                port = probe.getsockname()[1]
            process = subprocess.Popen([JAVA, "-cp", cp, "CompetitiveTestHost", str(port), str(tmp / "data")],
                                       cwd=tmp, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, text=True, encoding="utf-8")
            output = []
            threading.Thread(target=lambda: output.extend(process.stdout), daemon=True).start()
            for _ in range(100):
                try:
                    a = Client(port)
                    break
                except OSError:
                    if process.poll() is not None:
                        raise AssertionError("Test server did not start: " + "".join(output))
                    time.sleep(.1)
            else:
                raise TimeoutError("server start")
            b = Client(port)
            clients = [a, b]
            a.send("REGISTER", username="flow_host", password="test1234")
            a.wait("AUTH_OK")
            b.send("REGISTER", username="flow_guest", password="test1234")
            guest_auth = b.wait("AUTH_OK")
            outsider = Client(port)
            clients.append(outsider)
            outsider.send("REGISTER", username="flow_out", password="test1234")
            outsider.wait("AUTH_OK")
            for target, name in ((b, "flow_guest"), (outsider, "flow_out")):
                a.send("FRIEND_ADD", username=name)
                a.wait("FRIEND_REQUEST_SENT")
                target.send("FRIEND_REQUEST_LIST")
                request = target.wait("FRIEND_REQUEST_LIST", lambda m: bool(m.get("requests")))
                target.send("FRIEND_REQUEST_ACCEPT", requestId=rows(request, "requests")[0][0])
                target.wait("FRIEND_REQUEST_ACCEPTED")
            a.send("CREATE_ROOM", name="Classic private", max=2, puzzleId="sunset_3x3", password="secret", gameMode="CLASSIC", targetTime=0)
            room = a.wait("ROOM_SNAPSHOT")
            rid = room["roomId"]
            outsider.send("JOIN_ROOM", roomId="missing-room")
            assert outsider.wait("ERROR")["code"] == "ROOM_NOT_FOUND"
            b.send("JOIN_ROOM", roomId="  " + rid.lower() + "  ")
            assert b.wait("ERROR")["code"] == "PASSWORD_REQUIRED"
            b.send("JOIN_ROOM", roomId=rid, password="wrong")
            assert b.wait("ERROR")["code"] == "WRONG_PASSWORD"
            b.send("JOIN_ROOM", roomId=rid, password="secret")
            b.wait("ROOM_SNAPSHOT", lambda m: m["roomId"] == rid)
            outsider.send("JOIN_ROOM", roomId=rid, password="secret")
            assert outsider.wait("ERROR")["code"] == "ROOM_FULL"
            print("PASS: room IDs, trimmed/lowercase join, private password and full/missing errors")
            text = "Xin chào 😊👍🎉🔥❤️👏 <sprite=99>"
            b.send("CHAT", text=text)
            assert a.wait("CHAT")["text"] == text
            a.send("EMOJI", emoji="❤️")
            assert b.wait("EMOJI")["emoji"] == "❤️"
            a.send("CHAT", text="x" * 239 + "😊" + "z")
            assert b.wait("CHAT", lambda m: m.get("from") == "flow_host")["text"] == "x" * 239 + "😊"
            print("PASS: bidirectional Unicode/emoji and code-point-safe truncation")
            b.send("READY", ready="true")
            b.wait("ROOM_SNAPSHOT", lambda m: any(p[0] == "flow_guest" and p[2] == "true" for p in rows(m, "players")))
            a.send("START_GAME")
            game = a.wait("GAME_SNAPSHOT")
            b.wait("GAME_SNAPSHOT")
            a.send("LOCK_PIECE", pieceId=0)
            a.wait("PIECE_LOCKED")
            a.send("PLACE_PIECE", pieceId=0, placed="true")
            progress = a.wait("GAME_SNAPSHOT", lambda m: m["placed"] == "1")
            b.send("LEAVE_ROOM")
            b.wait("ROOM_LEFT")
            b.send("JOIN_ROOM", roomId=rid, password="secret")
            joined = b.wait("GAME_SNAPSHOT", lambda m: m["instanceId"] == game["instanceId"] and m["placed"] == "1")
            assert joined["themeId"] == game["themeId"] and joined["puzzleId"] == game["puzzleId"]
            assert next(p for p in rows(joined, "players") if p[0] == "flow_host")[3] == "1"
            b.send("LEAVE_ROOM")
            b.wait("ROOM_LEFT")
            a.send("INVITE", username="flow_guest")
            a.wait("PLAYER_EVENT", lambda m: "Đã gửi lời mời" in m.get("message", ""))
            assert b.wait("INVITE")["roomId"] == rid
            b.send("INVITE_RESPONSE", roomId=rid, accept="true")
            assert b.wait("GAME_SNAPSHOT")["instanceId"] == game["instanceId"]
            print("PASS: late CLASSIC join/invite preserves round, placed progress, score, theme and puzzle")
            for pid in range(1, 9):
                a.send("LOCK_PIECE", pieceId=pid)
                a.wait("PIECE_LOCKED", lambda m: m["pieceId"] == str(pid))
                a.send("PLACE_PIECE", pieceId=pid, placed="true")
                a.wait("PIECE_PLACED", lambda m: m["pieceId"] == str(pid))
            a.wait("GAME_COMPLETE")
            b.send("LEAVE_ROOM")
            b.wait("ROOM_LEFT")
            b.send("JOIN_ROOM", roomId=rid, password="secret")
            complete = b.wait("GAME_SNAPSHOT", lambda m: m.get("completed") == "true")
            assert complete["placed"] == "9" and complete["instanceId"] == game["instanceId"]
            b.send("LEAVE_ROOM")
            b.wait("ROOM_LEFT")
            a.send("LEAVE_ROOM")
            a.wait("ROOM_LEFT")
            a.send("CREATE_ROOM", name="Chaos", max=4, puzzleId="sunset_3x3", gameMode="COMPETITIVE")
            chaos_room = a.wait("ROOM_SNAPSHOT", lambda m: m["roomId"] != rid)
            cid = chaos_room["roomId"]
            b.send("JOIN_ROOM", roomId=cid)
            b.wait("ROOM_SNAPSHOT", lambda m: m["roomId"] == cid)
            b.send("READY", ready="true")
            b.wait("ROOM_SNAPSHOT", lambda m: any(p[0] == "flow_guest" and p[2] == "true" for p in rows(m, "players")))
            a.send("INVITE", username="flow_out")
            outsider.wait("INVITE", lambda m: m["roomId"] == cid)
            a.send("START_GAME")
            chaos = a.wait("GAME_SNAPSHOT", lambda m: m["instanceId"] != game["instanceId"])
            b.wait("GAME_SNAPSHOT", lambda m: m["instanceId"] == chaos["instanceId"])
            outsider.send("LOBBY_GET")
            listing = outsider.wait("LOBBY_SNAPSHOT", lambda m: all(r[0] != cid for r in rows(m, "rooms")))
            outsider.send("JOIN_ROOM", roomId=cid)
            assert outsider.wait("ERROR")["code"] == "GAME_ALREADY_STARTED"
            outsider.send("INVITE_RESPONSE", roomId=cid, accept="true")
            assert outsider.wait("ERROR")["code"] == "GAME_ALREADY_STARTED"
            print("PASS: active COMPETITIVE hidden from lobby, rejected by ID and old invite")
            b.close()
            time.sleep(.2)
            resumed = Client(port)
            clients.append(resumed)
            resumed.send("RESUME", token=guest_auth["token"])
            resumed.wait("RESUME_OK")
            resumed.wait("RECONNECT_PROMPT")
            resumed.send("RECONNECT_DECISION", roomId=cid, accept="true")
            assert resumed.wait("GAME_SNAPSHOT")["instanceId"] == chaos["instanceId"]
            print("PASS: reserved COMPETITIVE member reconnects to the same round")
        except Exception:
            if process is not None:
                print("Test server diagnostics:\n" + "".join(output[-25:]))
            for client in clients:
                print("Client diagnostics:", client.history[-40:])
            raise
        finally:
            for client in clients:
                client.close()
            if process is not None:
                process.terminate()
                process.wait(timeout=5)
            subprocess.run([*fixture, "drop", str(config), schema], check=True)


if __name__ == "__main__":
    main()
