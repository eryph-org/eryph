using System.Text.Json;
using System.Text.Json.Serialization;

namespace Eryph.Packer;

public class ImageInfo
{
    public string ImageName { get; }
    public string Organization { get; }
    public string Id { get; }

    public string Tag { get; }


    public ImageInfo(string image)
    {
        ImageName = image.ToLowerInvariant();
        var imageParts = image.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (imageParts.Length != 3 && imageParts.Length != 2)
        {
            throw new ArgumentException($"invalid image name '{image}'", nameof(image));
        }

        Organization = imageParts[0];
        Id = imageParts[1];
        Tag = imageParts.Length == 3 ? imageParts[2] : "latest";
    }

    public string GetImagePath()
    {
        if(File.Exists("manifest.json"))
        {
            var currentManifest = ReadManifest(".");
            if (currentManifest?.Image != ImageName)
            {
                if (currentManifest != null)
                {
                    throw new InvalidOperationException(
                        $"Directory already contains a manifest for image '{currentManifest.Image}' but you trying to access image '{ImageName}'. Make sure that you are in the right folder.");
                }
                throw new InvalidOperationException(
                    $"Directory already contains a invalid manifest.");

            }

            return ".";
        }

        return Path.Combine(Organization, Id, Tag);
    }

    public bool Exists()
    {

        if (!Directory.Exists(GetImagePath()))
            return false;

        if(!File.Exists(Path.Combine(GetImagePath(), "manifest.json")))
            return false;

        return true;
    }

    public void Create()
    {
        if (Exists())
            throw new InvalidOperationException($"Image {ImageName} already exists.");

        var directoryInfo = new DirectoryInfo(GetImagePath());
        if (!directoryInfo.Exists)
            directoryInfo.Create();

        if (!File.Exists(Path.Combine(GetImagePath(), "manifest.json")))
        {
            WriteManifest(new ImageManifest());
        }
    }

    public void EnsureCreated()
    {
        if(!Exists())
            Create();

    }

    public void SetReference(string referencedImage)
    {
        EnsureCreated();
        WriteManifest(new ImageManifest
        {
            Reference = referencedImage
        });
    }

    public void SetArtifacts(string[] artifacts)
    {
        EnsureCreated();
        WriteManifest(new ImageManifest
        {
            Artifacts = artifacts
        });
    }

    private void WriteManifest(ImageManifest manifest)
    {
        if (manifest.Image != ImageName)
            manifest.Image = ImageName;

        var jsonString = JsonSerializer.Serialize(manifest);
        File.WriteAllText(Path.Combine(GetImagePath(), "manifest.json"), jsonString);
    }

    public ImageManifest? GetManifest()
    {
        if(!Exists())
            return new ImageManifest { Image = ImageName };

        return ReadManifest(GetImagePath());
    }

    private ImageManifest? ReadManifest(string path)
    {
        try
        {
            var jsonString = File.ReadAllText(Path.Combine(path, "manifest.json"));

            var manifest = JsonSerializer.Deserialize<ImageManifest>(jsonString);
            return manifest;

        }
        catch
        {
            return new ImageManifest();
        }

    }

    public override string ToString()
    {
        return ToString(false);
    }

    public string ToString(bool pretty)
    {
        return JsonSerializer.Serialize(GetManifest(), new JsonSerializerOptions { WriteIndented = pretty });
    }
}

public class ImageManifest
{
    [JsonPropertyName("image")]
    public string Image { get; set; }

    [JsonPropertyName("ref")]
    public string Reference { get; set; }

    [JsonPropertyName("artifacts")]
    public string[] Artifacts { get; set; }

}