﻿using KcpServer.Lite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestServer.Lite
{
    class TestApplicationLite : ApplicationBase
    {
        public override PeerBase CreatePeer(PeerContext peerContext)
        {
            return new TestPeer(peerContext);
        }

        public override void Setup()
        {
            Console.WriteLine($"{nameof(TestApplicationLite)} {nameof(Setup)}");
        }

        public override void TearDown()
        {
            Console.WriteLine($"{nameof(TestApplicationLite)} {nameof(TearDown)}");
        }
    }
}
