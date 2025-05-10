
namespace Linda.Plants
{
	public class Token
	{
		public string Name;
		public string[] Args;

		public Token Clone()
		{
			Token copy = new Token();
			copy.Name = this.Name;
			copy.Args = this.Args != null ? (string[])this.Args.Clone() : null;
			return copy;
		}
	}
}
