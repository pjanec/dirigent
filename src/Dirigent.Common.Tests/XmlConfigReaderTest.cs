using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using NUnit.Framework;
 
using Dirigent.Common;

namespace DirigentCommons.Test
{
    [TestFixture]
    public class XmlConfigReaderTest
    {
        string xml = @"<?xml version=""1.0"" encoding=""UTF-8""?>
        <Shared>
            <Plan Name=""plan1"">
                <App
                    AppIdTuple = ""m1.a""
			        Template = ""apps.notepad""
			        StartupDir = ""c:\\""
			        CmdLineArgs = ""aaa.txt""
		        />

                <App
                    AppIdTuple = ""m1.b""
			        Template = ""apps.notepad""
			        StartupDir = ""c:\\""
			        CmdLineArgs = ""bbb.txt""
		        />
	        </Plan>

	        <AppTemplate Name=""apps.notepad""
			        Template = """"
		            ExeFullPath = ""c:\\windows\notepad.exe""
			        StartupDir = ""c:\\""
			        CmdLineArgs = """"
			        StartupOrder = ""0""
			        RestartOnCrash = ""1""
			        AdoptIfAlreadyRunning = ""1""
			        InitCondition = ""timeout 2.0""
			        SeparationInterval = ""0.5""
	        />

            <Machine Name=""m1"" IpAddress = ""127.0.0.1"" />

            <Master	Name = ""m1"" Port = ""12345"" />

         </Shared>
        ";

        
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Read1()
        {
            var r = new SharedXmlConfigReader();
            var cfg = r.Load( new StringReader(xml) );

            Assert.IsNotNull(cfg.Plans[0].getAppDefs());
            Assert.AreEqual( "m1.a", cfg.Plans[0].getAppDefs().First().AppIdTuple.ToString() );

        }
    }
}
