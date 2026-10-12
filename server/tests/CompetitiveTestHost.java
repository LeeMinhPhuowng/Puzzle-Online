import java.lang.reflect.*;
import java.nio.file.*;
import java.util.*;
import java.security.SecureRandom;

/** Runs the real server with deterministic gift rolls and a fast spawn clock, only for tests. */
public final class CompetitiveTestHost {
    public static void main(String[] args) throws Exception {
        Class<?> type = Class.forName("vn.puzzleonline.server.PuzzleServer");
        Constructor<?> ctor = type.getDeclaredConstructor(int.class, Path.class);
        ctor.setAccessible(true);
        Object server = ctor.newInstance(Integer.parseInt(args[0]), Path.of(args[1]));
        Field randomField = type.getDeclaredField("random");
        randomField.setAccessible(true);
        randomField.set(server, new SecureRandom() {
            int roll;
            @Override public int nextInt(int bound) {
                return bound == 100 ? new int[] { 0, 30, 55, 75, 99 }[roll++ % 5] : super.nextInt(bound);
            }
        });
        Field roomsField = type.getDeclaredField("rooms");
        roomsField.setAccessible(true);
        Field lockField = type.getDeclaredField("stateLock");
        lockField.setAccessible(true);
        Thread clock = new Thread(() -> {
            try {
                while (true) {
                    synchronized (lockField.get(server)) {
                        for (Object room : ((Map<?, ?>)roomsField.get(server)).values()) {
                            Field time = room.getClass().getDeclaredField("lastItemSpawn");
                            time.setAccessible(true);
                            time.setLong(room, 0);
                        }
                    }
                    Thread.sleep(100);
                }
            } catch (Exception error) { throw new RuntimeException(error); }
        });
        clock.setDaemon(true);
        clock.start();
        Method start = type.getDeclaredMethod("start");
        start.setAccessible(true);
        start.invoke(server);
    }
}
