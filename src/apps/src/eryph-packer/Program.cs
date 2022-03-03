// See https://aka.ms/new-console-template for more information

using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Invocation;
using System.CommandLine.IO;
using System.CommandLine.Parsing;
using System.CommandLine.Rendering;
using System.Diagnostics;
using Eryph.Packer;

var imageArgument = new Argument<string>("image", "name of image in format organization/id/[tag]");
var refArgument = new Argument<string>("referenced image", "name of referenced image in format organization/id/tag");
var vmExportArgument = new Argument<DirectoryInfo>("vm export", "path to exported VM");
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

var infoCommand = new Command("info", "This command reads the metadata of a image.");
infoCommand.AddArgument(imageArgument);
rootCommand.AddCommand(infoCommand);

var initCommand = new Command("init", "This command initializes the filesystem structure for a image.");
initCommand.AddArgument(imageArgument);
rootCommand.Add(initCommand);

var refCommand = new Command("ref", "This command adds a reference to another image to the image.");
refCommand.AddArgument(imageArgument);
refCommand.AddArgument(refArgument);
rootCommand.Add(refCommand);

var packCommand = new Command("pack", "This command packs a exported Virtual Machine into the image.");
packCommand.AddArgument(imageArgument);
packCommand.AddArgument(vmExportArgument);
rootCommand.Add(packCommand);

var pushCommand = new Command("push", "This command uploads a image to eryph-cloud");
pushCommand.AddArgument(imageArgument);
pushCommand.AddArgument(b2UploadArgument);
rootCommand.Add(pushCommand);



// init command
// ------------------------------
initCommand.SetHandler((string image, DirectoryInfo workdir) =>
{
    Directory.SetCurrentDirectory(workdir.FullName);
    
    var imageInfo = new ImageInfo(image);
    if (!imageInfo.Exists())
    {
        imageInfo.Create();
        Console.WriteLine(imageInfo.ToString(true));
        return;
    }

    throw new InvalidOperationException("Image already initialized.");
}, imageArgument, workDirOption);

// ref command
// ------------------------------
refCommand.SetHandler( (string image, string refImage, DirectoryInfo workdir) =>
{
    Directory.SetCurrentDirectory(workdir.FullName);

    var imageInfo = new ImageInfo(image);
    if (!imageInfo.Exists())
    {
        throw new InvalidOperationException($"Image {image} not found");
    }

    imageInfo.SetReference(refImage);
    Console.WriteLine(imageInfo.ToString(true));

}, imageArgument, refArgument, workDirOption);


// info command
// ------------------------------
infoCommand.SetHandler((string image, DirectoryInfo workdir) =>
{
    Directory.SetCurrentDirectory(workdir.FullName);

    var imageInfo = new ImageInfo(image);
    if (!imageInfo.Exists())
    {
        throw new InvalidOperationException($"Image {image} not found");
    }


    Console.WriteLine(imageInfo.ToString(true));    

}, imageArgument, workDirOption);


// pack command
// ------------------------------
packCommand.SetHandler(async (string image, DirectoryInfo vmExportDir, DirectoryInfo workdir, CancellationToken cancel) =>
{
    Directory.SetCurrentDirectory(workdir.FullName);

    var imageInfo = new ImageInfo(image);
    if (!imageInfo.Exists())
    {
        throw new InvalidOperationException($"Image {image} not found");
    }

    var vmPacker = new VMExportPacker();

    var absoluteImagePath = Path.GetFullPath(imageInfo.GetImagePath());
    var artifacts = await vmPacker.PackToArtifacts(vmExportDir, absoluteImagePath, cancel);

    imageInfo.SetArtifacts(artifacts.ToArray());
    Console.WriteLine(imageInfo.ToString(true));


}, imageArgument, vmExportArgument, workDirOption);


// push command
// ------------------------------
pushCommand.SetHandler(async (string image, FileInfo b2Uploader, DirectoryInfo workdir, CancellationToken cancel, InvocationContext context) =>
{
    try
    {
        Directory.SetCurrentDirectory(workdir.FullName);

        var imageInfo = new ImageInfo(image);
        if (!imageInfo.Exists())
        {
            throw new InvalidOperationException($"Image {image} not found");
        }

        context.Console.ResetTerminalForegroundColor();
        context.Console.WriteLine("This command is currently working only for internal testing and is not supported for general use.");
        context.Console.SetTerminalForegroundRed();
        context.Console.ResetTerminalForegroundColor();

        var imageDir = new DirectoryInfo(imageInfo.GetImagePath());
        foreach (var fileInfo in imageDir.GetFiles("*", SearchOption.AllDirectories))
        {
            var relativeName = Path.GetRelativePath(imageDir.FullName, fileInfo.FullName);
            Console.WriteLine($"Uploading: {relativeName}");

            var relativeUploadName =
                $"{imageInfo.Organization}/{imageInfo.Id}/{imageInfo.Tag}/{relativeName.Replace('\\', '/')}";

            var startedProcess = Process.Start(b2Uploader.FullName,
                $"upload-file eryph-images-staging \"{fileInfo.FullName}\" \"{relativeUploadName}\" ");
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

}, imageArgument, b2UploadArgument, workDirOption);

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