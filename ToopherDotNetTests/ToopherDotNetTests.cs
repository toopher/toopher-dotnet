using System;
using Toopher;
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
		[Test()]
		public void ToopherVersionTest ()
		{
			Assert.IsTrue(ToopherAPI.VERSION is string);
			string[] strs = ToopherAPI.VERSION.Split('.');
			int major = int.Parse(strs[0]);
			int minor = int.Parse(strs[1]);
			int patchLevel = int.Parse(strs[2]);
			Assert.IsTrue(major >= 1);
			Assert.IsTrue(minor >= 0);
			Assert.IsTrue(patchLevel >= 0);
		}
		[Test()]
		public void ToopherBaseUrlTest ()
		{
			Assert.IsTrue(ToopherAPI.DEFAULT_BASE_URL is string);
			Assert.IsTrue(System.Uri.IsWellFormedUriString(ToopherAPI.DEFAULT_BASE_URL, System.UriKind.Absolute));
		}
	}
}

