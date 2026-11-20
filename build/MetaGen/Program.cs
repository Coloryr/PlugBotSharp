using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

string? assemblyPath = null;
string? outputPath = null;
var typeName = "Meta";
var fieldName = "Info";

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--assembly":
            assemblyPath = args[++i];
            break;
        case "--output":
            outputPath = args[++i];
            break;
        case "--type":
            typeName = args[++i];
            break;
        case "--field":
            fieldName = args[++i];
            break;
        default:
            Console.Error.WriteLine($"未知参数: {args[i]}");
            return 1;
    }
}

if (assemblyPath is null || outputPath is null)
{
    Console.Error.WriteLine("用法: MetaGen --assembly <插件dll> --output <Meta.json> [--type Meta] [--field Info]");
    return 1;
}

var fullAssemblyPath = Path.GetFullPath(assemblyPath);

if (!File.Exists(fullAssemblyPath))
{
    Console.Error.WriteLine($"找不到程序集: {fullAssemblyPath}");
    return 1;
}

// 插件依赖（Core 等）在插件输出目录里，按文件名回退解析
var assemblyDir = Path.GetDirectoryName(fullAssemblyPath)!;

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    var candidate = Path.Combine(assemblyDir, name.Name + ".dll");
    return File.Exists(candidate) ? context.LoadFromAssemblyPath(candidate) : null;
};

Assembly assembly;

try
{
    assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullAssemblyPath);
}
catch (Exception e)
{
    Console.Error.WriteLine($"加载程序集失败: {e.Message}");
    return 1;
}

var type = assembly.GetType(typeName, throwOnError: false);

if (type is null)
{
    try
    {
        type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
    }
    catch (ReflectionTypeLoadException e)
    {
        type = e.Types.FirstOrDefault(t => t?.Name == typeName);
    }
}

if (type is null)
{
    Console.Error.WriteLine($"程序集里找不到类型: {typeName}");
    return 1;
}

object? info = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
    ?? type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

if (info is null)
{
    Console.Error.WriteLine($"{typeName}.{fieldName} 不存在或为 null");
    return 1;
}

var json = JsonSerializer.Serialize(info, info.GetType(), new JsonSerializerOptions
{
    WriteIndented = true,
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
});

var fullOutputPath = Path.GetFullPath(outputPath);
Directory.CreateDirectory(Path.GetDirectoryName(fullOutputPath)!);
File.WriteAllText(fullOutputPath, json, new UTF8Encoding(false));

Console.WriteLine($"Meta 生成: {fullOutputPath}");
return 0;
