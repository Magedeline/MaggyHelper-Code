using System.Security.Cryptography;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Celeste;

string[] inputPaths = ResolveInputPaths(args);

if (inputPaths.Length < 2)
{
    ShowUsage();
    WaitForExplorerLaunch();
    return 1;
}

string leftPath = Path.GetFullPath(inputPaths[0]);
string rightPath = Path.GetFullPath(inputPaths[1]);

if (!File.Exists(leftPath) || !File.Exists(rightPath))
{
    Console.Error.WriteLine("Both map paths must exist.");
    Console.Error.WriteLine($"Left:  {leftPath}");
    Console.Error.WriteLine($"Right: {rightPath}");
    WaitForExplorerLaunch();
    return 1;
}

BinaryPacker.Element left = ReadMap(leftPath);
BinaryPacker.Element right = ReadMap(rightPath);

MapSnapshot leftSnapshot = MapSnapshot.From(left);
MapSnapshot rightSnapshot = MapSnapshot.From(right);

var lines = new List<string>();
lines.Add($"Comparing {Path.GetFileName(leftPath)} -> {Path.GetFileName(rightPath)}");
lines.Add($"Package: {leftSnapshot.Package} -> {rightSnapshot.Package}");

AppendAttributeDiffs(lines, "Map attributes", leftSnapshot.Attributes, rightSnapshot.Attributes);
AppendTopLevelGroupDiffs(lines, leftSnapshot.TopLevelGroups, rightSnapshot.TopLevelGroups);
AppendRoomDiffs(lines, leftSnapshot.Rooms, rightSnapshot.Rooms);

if (lines.Count == 2)
{
    lines.Add("No semantic differences detected.");
}

foreach (string line in lines)
{
    Console.WriteLine(line);
}

WaitForExplorerLaunch();
return 0;

static void ShowUsage()
{
    Console.Error.WriteLine("MapDiff is a command-line map comparison tool.");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Usage:");
    Console.Error.WriteLine("  MapDiff.exe <left.bin> <right.bin>");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Examples:");
    Console.Error.WriteLine("  MapDiff.exe head-00_Prologue.bin Maps\\Maggy\\Main\\00_Prologue.bin");
    Console.Error.WriteLine();
    Console.Error.WriteLine("Tip:");
    Console.Error.WriteLine("  You can drag two .bin files onto MapDiff.exe in Explorer.");
    Console.Error.WriteLine("  If you launch it with no arguments on Windows, it will open file pickers.");
}

static void WaitForExplorerLaunch()
{
    if (Console.IsInputRedirected || Console.IsOutputRedirected)
    {
        return;
    }

    Console.WriteLine();
    Console.Write("Press any key to close...");
    Console.ReadKey(intercept: true);
    Console.WriteLine();
}

static string[] ResolveInputPaths(string[] args)
{
    if (args.Length >= 2)
    {
        return [args[0], args[1]];
    }

    if (!OperatingSystem.IsWindows())
    {
        return args;
    }

    string? presetLeftPath = args.Length == 1 && File.Exists(args[0])
        ? Path.GetFullPath(args[0])
        : null;

    return TryPickInputPaths(presetLeftPath, out string leftPath, out string rightPath)
        ? [leftPath, rightPath]
        : [];
}

static bool TryPickInputPaths(string? presetLeftPath, out string leftPath, out string rightPath)
{
    string selectedLeftPath = string.Empty;
    string selectedRightPath = string.Empty;
    Exception? dialogError = null;
    bool success = false;

    Thread thread = new(() =>
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            selectedLeftPath = presetLeftPath ?? PickMapFile(
                title: "Select the first Celeste map (.bin)",
                initialDirectory: GetDefaultMapDirectory(),
                fileName: string.Empty);

            if (string.IsNullOrEmpty(selectedLeftPath))
            {
                return;
            }

            selectedRightPath = PickMapFile(
                title: "Select the second Celeste map (.bin)",
                initialDirectory: Path.GetDirectoryName(selectedLeftPath) ?? GetDefaultMapDirectory(),
                fileName: Path.GetFileName(selectedLeftPath));

            success = !string.IsNullOrEmpty(selectedRightPath);
        }
        catch (Exception ex)
        {
            dialogError = ex;
        }
    });

    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (dialogError is not null)
    {
        throw dialogError;
    }

    leftPath = selectedLeftPath;
    rightPath = selectedRightPath;
    return success;
}

static string PickMapFile(string title, string initialDirectory, string fileName)
{
    using var dialog = new OpenFileDialog
    {
        Title = title,
        Filter = "Celeste binary maps (*.bin)|*.bin|All files (*.*)|*.*",
        InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : Environment.CurrentDirectory,
        FileName = fileName,
        CheckFileExists = true,
        CheckPathExists = true,
        Multiselect = false,
        RestoreDirectory = true
    };

    return dialog.ShowDialog() == DialogResult.OK ? dialog.FileName : string.Empty;
}

static string GetDefaultMapDirectory()
{
    string mapsDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Maps");
    return Path.GetFullPath(mapsDirectory);
}

static BinaryPacker.Element ReadMap(string path)
{
    using FileStream stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: false);

    string magic = reader.ReadString();
    if (!string.Equals(magic, "CELESTE MAP", StringComparison.Ordinal))
    {
        throw new InvalidDataException($"Unsupported map format header '{magic}'.");
    }

    string package = reader.ReadString();
    short lookupCount = reader.ReadInt16();
    string[] lookup = new string[lookupCount];
    for (int index = 0; index < lookupCount; index++)
    {
        lookup[index] = reader.ReadString();
    }

    BinaryPacker.Element root = ReadElement(reader, lookup);
    root.Package = package;
    return root;
}

static BinaryPacker.Element ReadElement(BinaryReader reader, string[] lookup)
{
    BinaryPacker.Element element = new();
    element.Name = lookup[reader.ReadInt16()];

    byte attributeCount = reader.ReadByte();
    if (attributeCount > 0)
    {
        element.Attributes = new Dictionary<string, object>(attributeCount);
    }

    for (int index = 0; index < attributeCount; index++)
    {
        string key = lookup[reader.ReadInt16()];
        byte type = reader.ReadByte();
        object? value = type switch
        {
            0 => reader.ReadBoolean(),
            1 => (int) reader.ReadByte(),
            2 => (int) reader.ReadInt16(),
            3 => reader.ReadInt32(),
            4 => reader.ReadSingle(),
            5 => lookup[reader.ReadInt16()],
            6 => reader.ReadString(),
            7 => ReadRunLengthEncoded(reader),
            _ => null
        };

        element.Attributes!.Add(key, value ?? string.Empty);
    }

    short childCount = reader.ReadInt16();
    if (childCount > 0)
    {
        element.Children = new List<BinaryPacker.Element>(childCount);
    }

    for (int index = 0; index < childCount; index++)
    {
        element.Children!.Add(ReadElement(reader, lookup));
    }

    return element;
}

static string ReadRunLengthEncoded(BinaryReader reader)
{
    short count = reader.ReadInt16();
    byte[] bytes = reader.ReadBytes(count);
    return RunLengthEncoding.Decode(bytes);
}

static void AppendTopLevelGroupDiffs(
    List<string> lines,
    IReadOnlyDictionary<string, NodeGroupSnapshot> leftGroups,
    IReadOnlyDictionary<string, NodeGroupSnapshot> rightGroups)
{
    foreach (string key in leftGroups.Keys.Union(rightGroups.Keys).OrderBy(static value => value, StringComparer.Ordinal))
    {
        leftGroups.TryGetValue(key, out NodeGroupSnapshot? leftGroup);
        rightGroups.TryGetValue(key, out NodeGroupSnapshot? rightGroup);

        if (leftGroup is null)
        {
            lines.Add($"Top-level group added: {key} ({rightGroup!.Nodes.Count} nodes)");
            continue;
        }

        if (rightGroup is null)
        {
            lines.Add($"Top-level group removed: {key} ({leftGroup.Nodes.Count} nodes)");
            continue;
        }

        AppendGroupDiffs(lines, $"Top-level group {key}", leftGroup, rightGroup);
    }
}

static void AppendRoomDiffs(
    List<string> lines,
    IReadOnlyDictionary<string, RoomSnapshot> leftRooms,
    IReadOnlyDictionary<string, RoomSnapshot> rightRooms)
{
    foreach (string roomName in leftRooms.Keys.Union(rightRooms.Keys).OrderBy(static value => value, StringComparer.Ordinal))
    {
        leftRooms.TryGetValue(roomName, out RoomSnapshot? leftRoom);
        rightRooms.TryGetValue(roomName, out RoomSnapshot? rightRoom);

        if (leftRoom is null)
        {
            lines.Add($"Room added: {roomName}");
            continue;
        }

        if (rightRoom is null)
        {
            lines.Add($"Room removed: {roomName}");
            continue;
        }

        int beforeCount = lines.Count;
        AppendAttributeDiffs(lines, $"Room {roomName} attributes", leftRoom.Attributes, rightRoom.Attributes, IsTileAttribute);
        AppendTileDiff(lines, roomName, "solids", leftRoom.Solids, rightRoom.Solids);
        AppendTileDiff(lines, roomName, "bg", leftRoom.Background, rightRoom.Background);
        AppendTileDiff(lines, roomName, "objTiles", leftRoom.ObjectTiles, rightRoom.ObjectTiles);

        foreach (string groupName in leftRoom.Groups.Keys.Union(rightRoom.Groups.Keys).OrderBy(static value => value, StringComparer.Ordinal))
        {
            leftRoom.Groups.TryGetValue(groupName, out NodeGroupSnapshot? leftGroup);
            rightRoom.Groups.TryGetValue(groupName, out NodeGroupSnapshot? rightGroup);

            if (leftGroup is null)
            {
                lines.Add($"Room {roomName} group added: {groupName} ({rightGroup!.Nodes.Count} nodes)");
                continue;
            }

            if (rightGroup is null)
            {
                lines.Add($"Room {roomName} group removed: {groupName} ({leftGroup.Nodes.Count} nodes)");
                continue;
            }

            AppendGroupDiffs(lines, $"Room {roomName} {groupName}", leftGroup, rightGroup);
        }

        if (lines.Count > beforeCount)
        {
            lines.Insert(beforeCount, $"Room changed: {roomName}");
        }
    }
}

static void AppendGroupDiffs(List<string> lines, string label, NodeGroupSnapshot leftGroup, NodeGroupSnapshot rightGroup)
{
    Dictionary<string, NodeSnapshot> leftIndex = leftGroup.Nodes.ToDictionary(static node => node.MatchKey, StringComparer.Ordinal);
    Dictionary<string, NodeSnapshot> rightIndex = rightGroup.Nodes.ToDictionary(static node => node.MatchKey, StringComparer.Ordinal);

    foreach (string key in leftIndex.Keys.Union(rightIndex.Keys).OrderBy(static value => value, StringComparer.Ordinal))
    {
        bool hasLeft = leftIndex.TryGetValue(key, out NodeSnapshot leftNode);
        bool hasRight = rightIndex.TryGetValue(key, out NodeSnapshot rightNode);

        if (!hasLeft)
        {
            lines.Add($"{label} added: {rightNode.Summary}");
            continue;
        }

        if (!hasRight)
        {
            lines.Add($"{label} removed: {leftNode.Summary}");
            continue;
        }

        var attrDiffs = GetAttributeDiffs(leftNode.Attributes, rightNode.Attributes, static key => string.Equals(key, "id", StringComparison.Ordinal));
        if (attrDiffs.Count == 0 && leftNode.ChildDigest == rightNode.ChildDigest)
        {
            continue;
        }

        if (attrDiffs.Count > 0)
        {
            foreach (string diff in attrDiffs)
            {
                lines.Add($"{label} changed: {leftNode.Summary} [{diff}]");
            }
        }

        if (leftNode.ChildDigest != rightNode.ChildDigest)
        {
            lines.Add($"{label} changed: {leftNode.Summary} [child structure updated]");
        }
    }
}

static void AppendTileDiff(List<string> lines, string roomName, string tileName, TileSnapshot left, TileSnapshot right)
{
    if (left.Hash == right.Hash)
    {
        return;
    }

    int changedCells = CountTileChanges(left.Raw, right.Raw);
    lines.Add(
        $"Room {roomName} {tileName} changed: {changedCells} cells, " +
        $"solid-count {left.FilledCells} -> {right.FilledCells}, size {left.Width}x{left.Height} -> {right.Width}x{right.Height}");
}

static void AppendAttributeDiffs(
    List<string> lines,
    string label,
    IReadOnlyDictionary<string, string> left,
    IReadOnlyDictionary<string, string> right,
    Func<string, bool>? shouldIgnore = null)
{
    foreach (string diff in GetAttributeDiffs(left, right, shouldIgnore))
    {
        lines.Add($"{label}: {diff}");
    }
}

static List<string> GetAttributeDiffs(
    IReadOnlyDictionary<string, string> left,
    IReadOnlyDictionary<string, string> right,
    Func<string, bool>? shouldIgnore = null)
{
    var diffs = new List<string>();

    foreach (string key in left.Keys.Union(right.Keys).OrderBy(static value => value, StringComparer.Ordinal))
    {
        if (shouldIgnore?.Invoke(key) == true)
        {
            continue;
        }

        bool hasLeft = left.TryGetValue(key, out string? leftValue);
        bool hasRight = right.TryGetValue(key, out string? rightValue);

        if (!hasLeft)
        {
            diffs.Add($"{key} added ({FormatValue(rightValue)})");
            continue;
        }

        if (!hasRight)
        {
            diffs.Add($"{key} removed ({FormatValue(leftValue)})");
            continue;
        }

        if (!string.Equals(leftValue, rightValue, StringComparison.Ordinal))
        {
            diffs.Add($"{key}: {FormatValue(leftValue)} -> {FormatValue(rightValue)}");
        }
    }

    return diffs;
}

static bool IsTileAttribute(string key)
{
    return string.Equals(key, "solids", StringComparison.Ordinal)
        || string.Equals(key, "bg", StringComparison.Ordinal)
        || string.Equals(key, "objTiles", StringComparison.Ordinal);
}

static string FormatValue(string? value)
{
    if (value is null)
    {
        return "<null>";
    }

    if (value.Length > 72)
    {
        return value[..69] + "...";
    }

    return value;
}

static int CountTileChanges(string left, string right)
{
    string[] leftRows = left.Split('\n');
    string[] rightRows = right.Split('\n');
    int maxRows = Math.Max(leftRows.Length, rightRows.Length);
    int changed = 0;

    for (int row = 0; row < maxRows; row++)
    {
        string leftRow = row < leftRows.Length ? leftRows[row] : string.Empty;
        string rightRow = row < rightRows.Length ? rightRows[row] : string.Empty;
        int maxCols = Math.Max(leftRow.Length, rightRow.Length);

        for (int col = 0; col < maxCols; col++)
        {
            char leftChar = col < leftRow.Length ? leftRow[col] : '0';
            char rightChar = col < rightRow.Length ? rightRow[col] : '0';
            if (leftChar != rightChar)
            {
                changed++;
            }
        }
    }

    return changed;
}

sealed class MapSnapshot
{
    public required string Package { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required IReadOnlyDictionary<string, RoomSnapshot> Rooms { get; init; }
    public required IReadOnlyDictionary<string, NodeGroupSnapshot> TopLevelGroups { get; init; }

    public static MapSnapshot From(BinaryPacker.Element root)
    {
        var rooms = new Dictionary<string, RoomSnapshot>(StringComparer.Ordinal);
        var groups = new Dictionary<string, NodeGroupSnapshot>(StringComparer.Ordinal);

        foreach (BinaryPacker.Element child in root.Children ?? new List<BinaryPacker.Element>())
        {
            if (string.Equals(child.Name, "levels", StringComparison.Ordinal))
            {
                foreach (BinaryPacker.Element level in child.Children ?? new List<BinaryPacker.Element>())
                {
                    RoomSnapshot room = RoomSnapshot.From(level);
                    rooms[room.Name] = room;
                }
            }
            else
            {
                groups[child.Name ?? string.Empty] = NodeGroupSnapshot.From(child);
            }
        }

        return new MapSnapshot
        {
            Package = root.Package ?? string.Empty,
            Attributes = MapDiffUtil.ToAttributeDictionary(root.Attributes),
            Rooms = rooms,
            TopLevelGroups = groups
        };
    }
}

sealed class RoomSnapshot
{
    public required string Name { get; init; }
    public required IReadOnlyDictionary<string, string> Attributes { get; init; }
    public required TileSnapshot Solids { get; init; }
    public required TileSnapshot Background { get; init; }
    public required TileSnapshot ObjectTiles { get; init; }
    public required IReadOnlyDictionary<string, NodeGroupSnapshot> Groups { get; init; }

    public static RoomSnapshot From(BinaryPacker.Element level)
    {
        Dictionary<string, string> attributes = MapDiffUtil.ToAttributeDictionary(level.Attributes);
        var groups = new Dictionary<string, NodeGroupSnapshot>(StringComparer.Ordinal);

        foreach (BinaryPacker.Element child in level.Children ?? new List<BinaryPacker.Element>())
        {
            groups[child.Name ?? string.Empty] = NodeGroupSnapshot.From(child);
        }

        return new RoomSnapshot
        {
            Name = attributes.TryGetValue("name", out string? roomName) ? roomName : "<unnamed>",
            Attributes = attributes,
            Solids = TileSnapshot.From(attributes.TryGetValue("solids", out string? solids) ? solids : string.Empty),
            Background = TileSnapshot.From(attributes.TryGetValue("bg", out string? background) ? background : string.Empty),
            ObjectTiles = TileSnapshot.From(attributes.TryGetValue("objTiles", out string? objectTiles) ? objectTiles : string.Empty),
            Groups = groups
        };
    }
}

sealed class NodeGroupSnapshot
{
    public required string Name { get; init; }
    public required IReadOnlyList<NodeSnapshot> Nodes { get; init; }

    public static NodeGroupSnapshot From(BinaryPacker.Element element)
    {
        var nodes = new List<NodeSnapshot>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (BinaryPacker.Element child in element.Children ?? new List<BinaryPacker.Element>())
        {
            NodeSnapshot node = NodeSnapshot.From(child);
            counts.TryGetValue(node.MatchKey, out int count);
            counts[node.MatchKey] = count + 1;
            if (count > 0)
            {
                node = node with { MatchKey = $"{node.MatchKey}#{count}" };
            }

            nodes.Add(node);
        }

        return new NodeGroupSnapshot
        {
            Name = element.Name ?? string.Empty,
            Nodes = nodes
        };
    }
}

readonly record struct NodeSnapshot(
    string MatchKey,
    string Summary,
    IReadOnlyDictionary<string, string> Attributes,
    string ChildDigest)
{
    public static NodeSnapshot From(BinaryPacker.Element element)
    {
        Dictionary<string, string> attributes = MapDiffUtil.ToAttributeDictionary(element.Attributes);
        string name = string.IsNullOrWhiteSpace(element.Name) ? "<anonymous>" : element.Name;
        string position = BuildPositionSuffix(attributes);
        string detail = attributes.TryGetValue("texture", out string? texture)
            ? $" texture={texture}"
            : string.Empty;

        return new NodeSnapshot(
            MatchKey: $"{name}{position}",
            Summary: $"{name}{position}{detail}",
            Attributes: attributes,
            ChildDigest: MapDiffUtil.DigestChildren(element.Children ?? new List<BinaryPacker.Element>()));
    }

    private static string BuildPositionSuffix(IReadOnlyDictionary<string, string> attributes)
    {
        bool hasX = attributes.TryGetValue("x", out string? x);
        bool hasY = attributes.TryGetValue("y", out string? y);
        bool hasWidth = attributes.TryGetValue("width", out string? width);
        bool hasHeight = attributes.TryGetValue("height", out string? height);

        if (!hasX && !hasY)
        {
            return string.Empty;
        }

        string size = hasWidth || hasHeight ? $" {width ?? "?"}x{height ?? "?"}" : string.Empty;
        return $" @({x ?? "?"},{y ?? "?"}{size})";
    }
}

sealed class TileSnapshot
{
    public required string Raw { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
    public required int FilledCells { get; init; }
    public required string Hash { get; init; }

    public static TileSnapshot From(string raw)
    {
        string[] rows = raw.Length == 0 ? Array.Empty<string>() : raw.Split('\n');
        int width = rows.Length == 0 ? 0 : rows.Max(static row => row.Length);
        int filledCells = 0;

        foreach (string row in rows)
        {
            foreach (char value in row)
            {
                if (value != '0' && value != '-')
                {
                    filledCells++;
                }
            }
        }

        return new TileSnapshot
        {
            Raw = raw,
            Width = width,
            Height = rows.Length,
            FilledCells = filledCells,
            Hash = MapDiffUtil.ComputeHash(raw)
        };
    }
}

static class MapDiffUtil
{
    public static Dictionary<string, string> ToAttributeDictionary(Dictionary<string, object>? attributes)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (attributes is null)
        {
            return result;
        }

        foreach ((string key, object value) in attributes)
        {
            result[key] = value?.ToString() ?? string.Empty;
        }

        return result;
    }

    public static string DigestChildren(List<BinaryPacker.Element> children)
    {
        if (children.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (BinaryPacker.Element child in children)
        {
            builder.Append(child.Name);
            builder.Append('|');
            foreach ((string key, string value) in ToAttributeDictionary(child.Attributes).OrderBy(static pair => pair.Key, StringComparer.Ordinal))
            {
                if (string.Equals(key, "id", StringComparison.Ordinal))
                {
                    continue;
                }

                builder.Append(key);
                builder.Append('=');
                builder.Append(value);
                builder.Append(';');
            }
            builder.Append('#');
        }

        return ComputeHash(builder.ToString());
    }

    public static string ComputeHash(string value)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes.AsSpan(0, 6));
    }
}