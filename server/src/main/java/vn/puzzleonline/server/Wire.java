package vn.puzzleonline.server;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Base64;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

final class Wire {
    static final char RECORD_SEPARATOR = 0x1e;
    static final char FIELD_SEPARATOR = 0x1f;

    private Wire() {}

    static Message decode(String line) {
        String[] parts = line.split("\\|", -1);
        Map<String, String> values = new LinkedHashMap<>();
        for (int i = 1; i < parts.length; i++) {
            int separator = parts[i].indexOf('=');
            if (separator <= 0) continue;
            String key = parts[i].substring(0, separator);
            String encoded = parts[i].substring(separator + 1);
            try {
                values.put(key, new String(Base64.getUrlDecoder().decode(encoded), StandardCharsets.UTF_8));
            } catch (IllegalArgumentException ignored) {
                values.put(key, "");
            }
        }
        return new Message(parts.length == 0 ? "" : parts[0].toUpperCase(Locale.ROOT), values);
    }

    static String encode(String type, Object... keyValues) {
        StringBuilder output = new StringBuilder(type == null ? "" : type);
        for (int i = 0; i + 1 < keyValues.length; i += 2) {
            String key = String.valueOf(keyValues[i]);
            String value = format(keyValues[i + 1]);
            output.append('|').append(key).append('=').append(
                    Base64.getUrlEncoder().withoutPadding().encodeToString(value.getBytes(StandardCharsets.UTF_8)));
        }
        return output.toString();
    }

    static String pack(List<List<?>> rows) {
        List<String> packedRows = new ArrayList<>();
        for (List<?> row : rows) {
            List<String> packedFields = new ArrayList<>();
            for (Object field : row) packedFields.add(format(field));
            packedRows.add(String.join(String.valueOf(FIELD_SEPARATOR), packedFields));
        }
        return String.join(String.valueOf(RECORD_SEPARATOR), packedRows);
    }

    static String format(Object value) {
        if (value == null) return "";
        if (value instanceof Float || value instanceof Double)
            return String.format(Locale.ROOT, "%.6f", ((Number) value).doubleValue());
        return String.valueOf(value);
    }

    static final class Message {
        final String type;
        final Map<String, String> values;

        Message(String type, Map<String, String> values) {
            this.type = type;
            this.values = values;
        }

        String get(String key) { return values.getOrDefault(key, ""); }
        int integer(String key, int fallback) {
            try { return Integer.parseInt(get(key)); } catch (NumberFormatException ignored) { return fallback; }
        }
        float decimal(String key, float fallback) {
            try { return Float.parseFloat(get(key)); } catch (NumberFormatException ignored) { return fallback; }
        }
        boolean bool(String key, boolean fallback) {
            String value = get(key);
            if (value.equalsIgnoreCase("true") || value.equals("1")) return true;
            if (value.equalsIgnoreCase("false") || value.equals("0")) return false;
            return fallback;
        }
    }
}
