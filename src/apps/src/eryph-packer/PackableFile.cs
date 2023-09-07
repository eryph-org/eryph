namespace Eryph.Packer;

public record PackableFile(string FullPath, string FileName, GeneType GeneType, string GeneName, bool ExtremeCompression);