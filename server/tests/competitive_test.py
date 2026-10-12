"""Two real TCP clients, real Java server, disposable MySQL schema. Run with python server/tests/competitive_test.py."""
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
            a.send("REGISTER", username="test_host", password="test1234")
            a.wait("AUTH_OK")
            b.send("REGISTER", username="test_guest", password="test1234")
            b.wait("AUTH_OK")
            a.send("CREATE_ROOM", name="Competitive regression", max=2, puzzleId="sunset_3x3", gameMode="COMPETITIVE")
            room = a.wait("ROOM_SNAPSHOT")
            b.send("JOIN_ROOM", roomId=room["roomId"])
            room = b.wait("ROOM_SNAPSHOT")
            assert next(p for p in rows(room, "players") if p[0] == "test_guest")[2] == "false"
            a.send("READY", ready="false")
            room = a.wait("ROOM_SNAPSHOT", lambda m: rows(m, "players")[0][2] == "false")
            assert all(p[2] == "false" for p in rows(room, "players")), "unready host/guest should be black"
            b.send("READY", ready="true")
            room = a.wait("ROOM_SNAPSHOT", lambda m: any(p[0] == "test_guest" and p[2] == "true" for p in rows(m, "players")))
            assert rows(room, "players")[0][2] == "false", "host start exemption must not fake ready"
            a.send("START_GAME")
            game = a.wait("GAME_SNAPSHOT")
            b.wait("GAME_SNAPSHOT")
            players = rows(game, "players")
            users = {"test_host": a, "test_guest": b}
            sab_user = next(p[0] for p in players if p[6] == "SABOTEUR")
            builder_user = next(p[0] for p in players if p[6] == "BUILDER")
            sab, builder = users[sab_user], users[builder_user]
            sab.send("USE_SABOTAGE", itemType="FLASHLIGHT", targetUser=builder_user)
            assert sab.wait("ERROR")["code"] == "ITEM_NOT_OWNED"

            # Place one piece for the shovel; use another piece to verify forced release.
            zero = rows(game, "pieces")[0]
            builder.send("LOCK_PIECE", pieceId=0)
            builder.wait("PIECE_LOCKED")
            builder.send("MOVE_PIECE", pieceId=0, x=1/6, y=1/6)
            for _ in range(((360 - int(zero[5])) % 360) // 90):
                builder.send("ROTATE_PIECE", pieceId=0, degrees=90)
            builder.send("PLACE_PIECE", pieceId=0)
            builder.wait("PIECE_PLACED")
            expected = ["FLASHLIGHT", "INK", "SLAP", "MAGNET", "SHOVEL"]
            for item in expected:
                gift_snapshot = sab.wait("GAME_SNAPSHOT", lambda m: bool(rows(m, "items")))
                gift = {"itemId": rows(gift_snapshot, "items")[0][0]}
                builder.send("PICKUP_ITEM", itemId=gift["itemId"])
                assert builder.wait("ERROR")["code"] == "ONLY_SABOTEUR_PICKUP"
                sab.send("PICKUP_ITEM", itemId=gift["itemId"])
                picked = sab.wait("ITEM_PICKED_UP")
                assert picked["type"] == item
                builder.wait("ITEM_PICKED_UP", lambda m: m["itemId"] == gift["itemId"])
                held = sab.wait("GAME_SNAPSHOT", lambda m: dict(rows(m, "heldItems")).get(sab_user) == item)
                if item in ("FLASHLIGHT", "SLAP", "MAGNET"):
                    sab.send("USE_SABOTAGE", itemType=item, targetUser=sab_user)
                    assert sab.wait("ERROR")["code"] == "INVALID_TARGET"
                    held = sab.wait("GAME_SNAPSHOT")
                    assert dict(rows(held, "heldItems"))[sab_user] == item
                    if item in ("SLAP", "MAGNET"):
                        builder.send("LOCK_PIECE", pieceId=1)
                        builder.wait("PIECE_LOCKED", lambda m: m["pieceId"] == "1")
                    sab.send("USE_SABOTAGE", itemType=item, targetUser=builder_user)
                    event = builder.wait("SABOTAGE_TRIGGERED", lambda m: m["item"] == item)
                    assert event["target"] == builder_user and event["by"] == sab_user
                elif item == "SHOVEL":
                    sab.send("USE_SABOTAGE", itemType=item, pieceId=8)
                    assert sab.wait("ERROR")["code"] == "PIECE_NOT_PLACED"
                    sab.send("USE_SABOTAGE", itemType=item, pieceId=0)
                    builder.wait("PIECE_UNFIT", lambda m: m["pieceId"] == "0")
                else:
                    sab.send("USE_SABOTAGE", itemType=item)
                    builder.wait("INK_SPLASH")
                sab.wait("ITEM_USED", lambda m: m["item"] == item)
                builder.wait("ITEM_USED", lambda m: m["item"] == item)
                empty = sab.wait("GAME_SNAPSHOT")
                assert dict(rows(empty, "heldItems"))[sab_user] == ""
                sab.send("USE_SABOTAGE", itemType=item, targetUser=builder_user, pieceId=0)
                assert sab.wait("ERROR")["code"] == "ITEM_NOT_OWNED", "duplicate use should fail"
                print("PASS:", item, "pickup, target/effect broadcast, confirmed consumption and duplicate rejection")

            a.send("RESTART_GAME")
            restarted = a.wait("GAME_SNAPSHOT", lambda m: m["instanceId"] != game["instanceId"])
            assert all(not p[1] for p in rows(restarted, "heldItems"))
            print("PASS: two-client ready state, authoritative inventory, five sabotage types and restart")
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
