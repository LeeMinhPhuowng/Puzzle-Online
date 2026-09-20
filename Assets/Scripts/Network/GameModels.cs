using System;
using System.Collections.Generic;
using System.Globalization;

namespace PuzzleOnline.Network
{
    [Serializable]
    public sealed class PlayerInfo
    {
        public string Username;
        public string Status;
        public bool Ready;
        public int Score;
        public bool Connected;
        public bool IsHost;
    }

    [Serializable]
    public sealed class FriendInfo
    {
        public string Username;
        public string Status;
        public bool Online;
    }

    [Serializable]
    public sealed class RoomInfo
    {
        public string Id;
        public string Name;
        public string Host;
        public int Count;
        public int MaxPlayers;
        public string State;
        public string PuzzleId;
        public string ThemeId = "SCN_Nature";
        public bool HasSave;
        public bool HasPassword;
        public int TargetTimeSeconds = 300;
    }

    [Serializable]
    public sealed class ReconnectPromptInfo
    {
        public string RoomId;
        public string RoomName;
        public int Count;
        public int Max;
        public string State;
        public string PuzzleId;
    }

    [Serializable]
    public sealed class PuzzleDefinition
    {
        public string Id;
        public string Name;
        public int Rows;
        public int Columns;
        public int Seconds;
        public int Price;
        public bool Owned;
    }

    [Serializable]
    public sealed class ThemeDefinition
    {
        public string Id;          // Scene name: SCN_Nature, SCN_Classroom, SCN_Sakura
        public string Name;        // Display name
        public int Price;
        public bool Owned;
        public bool Equipped;
    }

    [Serializable]
    public sealed class PieceInfo
    {
        public int Id;
        public int Row;
        public int Column;
        public float X;
        public float Y;
        public int Rotation;
        public bool Placed;
        public string LockedBy;
    }

    [Serializable]
    public sealed class FriendRequestItem
    {
        public long RequestId;
        public long SenderId;
        public string SenderUsername;
        public string CreatedAt;
    }

    public static class ModelParser
    {
        public static List<RoomInfo> Rooms(string packed)
        {
            var output = new List<RoomInfo>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 8) continue;
                output.Add(new RoomInfo
                {
                    Id = fields[0],
                    Name = fields[1],
                    Host = fields[2],
                    Count = Int(fields[3]),
                    MaxPlayers = Int(fields[4]),
                    State = fields[5],
                    PuzzleId = fields[6],
                    HasSave = Bool(fields[7]),
                    ThemeId = fields.Length >= 9 ? fields[8] : "SCN_Nature",
                    HasPassword = fields.Length >= 10 && Bool(fields[9]),
                    TargetTimeSeconds = fields.Length >= 11 ? Int(fields[10]) : 300
                });
            }
            return output;
        }

        public static List<FriendInfo> Friends(string packed)
        {
            var output = new List<FriendInfo>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 3) continue;
                output.Add(new FriendInfo { Username = fields[0], Status = fields[1], Online = Bool(fields[2]) });
            }
            return output;
        }

        public static List<FriendRequestItem> FriendRequests(string packed)
        {
            var output = new List<FriendRequestItem>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 3) continue;
                output.Add(new FriendRequestItem
                {
                    RequestId = Long(fields[0]),
                    SenderId = Long(fields[1]),
                    SenderUsername = fields[2],
                    CreatedAt = fields.Length >= 4 ? fields[3] : ""
                });
            }
            return output;
        }

        public static List<PlayerInfo> Players(string packed)
        {
            var output = new List<PlayerInfo>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 6) continue;
                output.Add(new PlayerInfo
                {
                    Username = fields[0],
                    Status = fields[1],
                    Ready = Bool(fields[2]),
                    Score = Int(fields[3]),
                    Connected = Bool(fields[4]),
                    IsHost = Bool(fields[5])
                });
            }
            return output;
        }

        public static List<PuzzleDefinition> Puzzles(string packed)
        {
            var output = new List<PuzzleDefinition>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 7) continue;
                output.Add(new PuzzleDefinition
                {
                    Id = fields[0],
                    Name = fields[1],
                    Rows = Int(fields[2]),
                    Columns = Int(fields[3]),
                    Seconds = Int(fields[4]),
                    Price = Int(fields[5]),
                    Owned = Bool(fields[6])
                });
            }
            return output;
        }

        public static List<ThemeDefinition> Themes(string packed)
        {
            var output = new List<ThemeDefinition>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 5) continue;
                output.Add(new ThemeDefinition
                {
                    Id = fields[0],
                    Name = fields[1],
                    Price = Int(fields[2]),
                    Owned = Bool(fields[3]),
                    Equipped = Bool(fields[4])
                });
            }
            return output;
        }

        public static List<PieceInfo> Pieces(string packed)
        {
            var output = new List<PieceInfo>();
            foreach (var fields in WireProtocol.UnpackRecords(packed))
            {
                if (fields.Length < 8) continue;
                output.Add(new PieceInfo
                {
                    Id = Int(fields[0]),
                    Row = Int(fields[1]),
                    Column = Int(fields[2]),
                    X = Float(fields[3]),
                    Y = Float(fields[4]),
                    Rotation = Int(fields[5]),
                    Placed = Bool(fields[6]),
                    LockedBy = fields[7]
                });
            }
            return output;
        }

        private static int Int(string value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        private static long Long(string value) => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0L;
        private static float Float(string value) => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0f;
        private static bool Bool(string value) => value == "true" || value == "1";
    }
}
