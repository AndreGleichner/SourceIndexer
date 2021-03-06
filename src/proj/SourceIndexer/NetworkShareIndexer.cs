namespace SourceIndexer
{
	using System;
	using System.Collections.Generic;
	using System.IO;
	using System.Linq;

	public class NetworkShareIndexer : ISourceIndexer
	{
		private const string SourceFileKey = "SRCSRVSRC";
		private const string SourceFileValue = "%published_artifacts_src%\\%var2%\\%fnfile%(%var1%)";
		private const string ResolvedFilePath = "%targ%\\%var2%\\%fnfile%(%var1%)";
		private const string ResolveFileCommand = "cmd.exe /c copy /y \"%srcsrvsrc%\" %srcsrvtrg%";
		private static readonly IDictionary<string, string> Hashes = new Dictionary<string, string>();
		private readonly string sourceControlPath;

		public NetworkShareIndexer(string sourceControlPath)
		{
			this.sourceControlPath = sourceControlPath;
		}

		public void Index(DebugSymbol symbol)
		{
			var stream = this.GetStreamToEmbed(symbol);
			symbol.Write(stream);
		}

		private EmbeddedSymbolStream GetStreamToEmbed(DebugSymbol symbol)
		{
			var variables = new Dictionary<string, string>();
			variables[SourceFileKey] = SourceFileValue;
			var sourceFiles = this.GetSourceFiles(symbol).ToArray();

			return new EmbeddedSymbolStream(variables, ResolvedFilePath, ResolveFileCommand, sourceFiles);
		}

		private IEnumerable<SourceFile> GetSourceFiles(DebugSymbol symbol)
		{
			return symbol.SourceFiles.Select(file => new SourceFile(file, this.GetHash(file)));
		}

		private string GetHash(string filename)
		{
			string hash;
			if (Hashes.TryGetValue(filename, out hash))
				return hash;

			using (Stream inputStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
				Hashes[filename] = hash = inputStream.ComputeHash().FormatHash();

			this.PushToSourceControl(filename, hash);

			return hash;
		}

		private void PushToSourceControl(string filename, string hash)
		{
			if (string.IsNullOrEmpty(this.sourceControlPath))
				return;

			var destination = Path.Combine(this.sourceControlPath, hash, Path.GetFileName(filename) ?? string.Empty);
			if (File.Exists(destination))
				return;

			Console.WriteLine(string.Format("Pushing '{0}' into source control system at '{1}'.", filename, destination));
			var directory = Path.GetDirectoryName(destination) ?? string.Empty;
			Directory.CreateDirectory(directory);
			File.Copy(filename, destination, true);
		}
	}
}