namespace Irony.Parsing
{
	/// <summary>
	/// Should be implemented by <see cref="Grammar"/> class to be able to run samples in Grammar Explorer.
	/// </summary>
	public interface ICanRunSample
	{
		string RunSample(RunSampleArgs args);
	}

	public class RunSampleArgs
	{
		public LanguageData Language;
		public ParseTree ParsedSample;
		public string Sample;

		public RunSampleArgs(LanguageData language, string sample, ParseTree parsedSample)
		{
			this.Language = language;
			this.Sample = sample;
			this.ParsedSample = parsedSample;
		}
	}
}
