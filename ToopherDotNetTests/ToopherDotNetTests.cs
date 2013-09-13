using System;
using NUnit.Framework;

namespace ToopherDotNetTests
{
	[TestFixture()]
	public class Test
	{
		[Test()]
		public void SimpleTest ()
		{
			Assert.AreEqual(1, 1);
		}
	}
}

