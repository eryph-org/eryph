// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.CommandLine.Rendering;
using System.Diagnostics;
using System.Text.Json;
using Eryph.ConfigModel.Catlets;
using Eryph.ConfigModel.Json;
using Eryph.ConfigModel.Yaml;
using Eryph.Packer;
using YamlDotNet.Core;

var genesetArgument = new Argument<string>("geneset", "name of geneset in format organization/id/[tag]");
var refArgument = new Argument<string>("referenced geneset", "name of referenced geneset in format organization/id/tag");
var vmExportArgument = new Argument<DirectoryInfo>("vm export", "path to exported VM");
var filePathArgument = new Argument<FileInfo>("file", "path to file");

var b2UploadArgument = new Argument<FileInfo>("b2 upload");

vmExportArgument.ExistingOnly();

var debugOption =
    new Option<bool>("--debug", "Enables debug output.");


var workDirOption =
    new Option<DirectoryInfo>("--workdir", "work directory")
        .ExistingOnly();
    
workDirOption.SetDefaultValue(new DirectoryInfo(Environment.CurrentDirectory));


var rootCommand = new RootCommand();
rootCommand.AddGlobalOption(workDirOption);
rootCommand.AddGlobalOption(debugOption);

var infoCommand = new Command("info", "This command reads the metadata of a geneset.");
infoCommand.AddArgument(genesetArgument);
rootCommand.AddCommand(infoCommand);

var initCommand = new Command("init", "This command initializes the filesystem structure for a geneset.");
initCommand.AddArgument(genesetArgument);
rootCommand.Add(initCommand);

var refCommand = new Command("ref", "This command adds a reference to another geneset to the geneset.");
refCommand.AddArgument(genesetArgument);
refCommand.AddArgument(refArgument);
rootCommand.Add(refCommand);

var packCommand = new Command("pack", "This command packs genes into a geneset");

var packVMCommand = new Command("vm", "This command packs a exported Hyper-V VM into the geneset.");
packVMCommand.AddArgument(genesetArgument); 
packVMCommand.AddArgument(vmExportArgument);
packCommand.AddCommand(packVMCommand);

var packCatletCommand = new Command("catlet", "This command packs a catlet gene into the geneset.");
packCatletCommand.AddArgument(filePathArgument);
packCatletCommand.AddArgument(genesetArgument);
packCommand.AddCommand(packCatletCommand);

var packVolumeCommand = new Command("volume", "This command packs a volume gene into the geneset.");
packVolumeCommand.AddArgument(genesetArgument);
packVolumeCommand.AddArgument(filePathArgument);
packCommand.AddCommand(packVolumeCommand);

var packFodderCommand = new Command("fodder", "This command packs a fodder gene into the geneset.");
packFodderCommand.AddArgument(genesetArgument);
packFodderCommand.AddArgument(filePathArgument);
packCommand.AddCommand(packFodderCommand);

rootCommand.Add(packCommand);

var pushCommand = new Command("push", "This command uploads a geneset to eryph genepool");
pushCommand.AddArgument(genesetArgument);
pushCommand.AddArgument(b2UploadArgument);
rootCommand.Add(pushCommand);



// init command
// ------------------------------
initCommand.SetHandler((genesetName, workdir) =>
{
    Directory.SetCurrentDirectory(workdir.FullName);
    
    var genesetInfo = new GeneSetInfo(genesetName);
    if (!genesetInfo.Exists())
    {
        genesetInfo.Create();
        Console.WriteLine(genesetInfo.ToString(true));
        return;
    }

    throw new InvalidOperationException("Geneset already initialized.");
}, genesetArgument, workDirOption);

// ref command
// ------------------------------
refCommand.SetHandler( async (context) =>
{
    var genesetInfo = PrepareGeneSetCommand(context);
    var refPack = context.ParseResult.GetValueForArgument(refArgument);
    genesetInfo.SetReference(refPack);
    Console.WriteLine(genesetInfo.ToString(true));

});


// info command
// ------------------------------
infoCommand.SetHandler((context) =>
{
    var genesetInfo = PrepareGeneSetCommand(context);
    Console.WriteLine(genesetInfo.ToString(true));    

});


// pack vm command
// ------------------------------
packVMCommand.SetHandler(async context =>
{
    var token = context.GetCancellationToken();
    var genesetInfo = PrepareGeneSetCommand(context);
    var vmExportDir = context.ParseResult.GetValueForArgument(vmExportArgument);
    var metadata = new Dictionary<string, string>();
    var metadataFile = new FileInfo(Path.Combine(vmExportDir.FullName, "metadata.json"));
    if (metadataFile.Exists)
    {
        try
        {
            await using var metadataStream = metadataFile.OpenRead();
            var newMetadata = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(metadataStream) 
                              ?? metadata;
            genesetInfo.JoinMetadata(newMetadata);
        }
        catch (Exception ex)
        {
            throw new Exception("failed to read metadata.json file included in exported vm", ex);
        }
    }

    var absolutePackPath = Path.GetFullPath(genesetInfo.GetGeneSetPath());
    var packableFiles = await VMExport.ExportToPackable(vmExportDir, absolutePackPath, token);
    foreach (var packableFile in packableFiles)
    {
        var geneHash = await GenePacker.CreateGene(packableFile, absolutePackPath, new Dictionary<string, string>(), token);
        genesetInfo.AddGene(packableFile.GeneType, packableFile.GeneName, geneHash);
    }

    Console.WriteLine(genesetInfo.ToString(true));


});

// pack catlet command
// ------------------------------
packCatletCommand.SetHandler(async context =>
{
    var token = context.GetCancellationToken();
    var genesetInfo = PrepareGeneSetCommand(context);
    var catletFile = context.ParseResult.GetValueForArgument(filePathArgument);
    var absolutePackPath = Path.GetFullPath(genesetInfo.GetGeneSetPath());

    var catletContent = File.ReadAllText(catletFile.FullName);
    var (jsonFile, parsedConfig) = DeserializeConfigString(catletContent);
    genesetInfo.SetParent(parsedConfig.Parent);

    if (jsonFile)
    {
        var configYaml = CatletConfigYamlSerializer.Serialize(parsedConfig);
        var catletYamlFilePath = Path.Combine(absolutePackPath, "catlet.yaml");
        await File.WriteAllTextAsync(catletYamlFilePath, configYaml);
    }
    else
    {
        File.Copy(catletFile.FullName, Path.Combine(absolutePackPath, "catlet.yaml"));
    }

    var configJson = ConfigModelJsonSerializer.Serialize(parsedConfig);
    var catletJsonFilePath = Path.Combine(absolutePackPath, "catlet.json");
    await File.WriteAllTextAsync(catletJsonFilePath, configJson);


    var packedFile =
        await GenePacker.CreateGene(
            new PackableFile(catletJsonFilePath, "catlet.json", GeneType.Catlet, "catlet", false),
            absolutePackPath, new Dictionary<string, string>(), token);

    genesetInfo.AddGene(GeneType.Catlet, "catlet", packedFile);
    Console.WriteLine(genesetInfo.ToString(true));


});


// push command
// ------------------------------
pushCommand.SetHandler(async context =>
{
    var b2Uploader = context.ParseResult.GetValueForArgument(b2UploadArgument);
    var token = context.GetCancellationToken();
    var genesetInfo = PrepareGeneSetCommand(context);

    try
    {

        context.Console.ResetTerminalForegroundColor();
        context.Console.WriteLine("This command is currently working only for internal testing and is not supported for general use.");
        context.Console.SetTerminalForegroundRed();
        context.Console.ResetTerminalForegroundColor();

        var packDir = new DirectoryInfo(genesetInfo.GetGeneSetPath());
        foreach (var fileInfo in packDir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(packDir.FullName, fileInfo.FullName);
            Console.WriteLine($"Uploading: {relativeName}");

            var relativeUploadName =
                $"{genesetInfo.Organization}/{genesetInfo.Id}/{genesetInfo.Tag}/{relativeName.Replace('\\', '/')}";

            var startedProcess = Process.Start(b2Uploader.FullName,
                $"upload-file eryph-staging-eu \"{fileInfo.FullName}\" \"{relativeUploadName}\" ");
            await startedProcess.WaitForExitAsync();

            if (startedProcess.ExitCode != 0)
            {
                Console.WriteLine($"Upload of file {relativeName} failed");
                return;
            }
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
    }

});

var commandLineBuilder = new CommandLineBuilder(rootCommand);
commandLineBuilder.UseDefaults();
commandLineBuilder.UseAnsiTerminalWhenAvailable();
commandLineBuilder.UseExceptionHandler((ex, context) =>
{
    if (ex is not OperationCanceledException)
    {
        context.Console.ResetTerminalForegroundColor();
        context.Console.SetTerminalForegroundRed();


        if (context.BindingContext.ParseResult.HasOption(debugOption))
        {
            context.Console.Error.Write(context.LocalizationResources.ExceptionHandlerHeader());
            context.Console.Error.WriteLine(ex.ToString());
        }
        else
        {
            context.Console.Error.WriteLine(ex.Message);
        }

        context.Console.ResetTerminalForegroundColor();
    }

    context.ExitCode = 1;

});

var parser = commandLineBuilder.Build();

return await parser.InvokeAsync(args);


GeneSetInfo PrepareGeneSetCommand(InvocationContext context)
{
    var workdir = context.ParseResult.GetValueForOption(workDirOption!);
    var genesetName = context.ParseResult.GetValueForArgument(genesetArgument!);

    if(workdir?.FullName != null)
        Directory.SetCurrentDirectory(workdir.FullName);

    var genesetInfo = new GeneSetInfo(genesetName);
    if (!genesetInfo.Exists())
    {
        throw new InvalidOperationException($"Geneset {genesetName} not found");
    }

    return genesetInfo;
}

static (bool Json, CatletConfig Config) DeserializeConfigString(string configString)
{
    configString = configString.Trim();
    configString = configString.Replace("\r\n", "\n");

    if (configString.StartsWith("{") && configString.EndsWith("}"))
        return (true,CatletConfigDictionaryConverter.Convert(ConfigModelJsonSerializer.DeserializeToDictionary(configString)));

    //YAML
    try
    {
        return (false, CatletConfigYamlSerializer.Deserialize(configString));
    }
    catch (YamlException ex)
    {
        throw ex;
    }

}