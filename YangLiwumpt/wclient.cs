/*
    WUMP (specifically BUMP) C# starter file
 */

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
//using System.Net.Sockets.UdpClient;
using System.Diagnostics;
using System.Threading;

public class wclient
{

    //static wumppkt wp = new wumppkt();    // stupid inner-class nonsense

    //============================================================
    //============================================================

    static public void Main(string[] args)
    {
        int destport = wumppkt.SERVERPORT;
        destport = wumppkt.SAMEPORT;      // 4716; server responds from same port
        string filename = "reorder";
        string desthost = "ulam1.cs.luc.edu";
        int winsize = 2;

      // int latchport = 0;          // to record port of arriving Data[1], if desired

        if (args.Length > 0) filename = args[0];
        if (args.Length > 1) Int32.TryParse(args[1], out winsize);
        if (args.Length > 2) desthost = args[2];
        if (args.Length > 3)
        {
            Console.Error.WriteLine("usage: wclient filename  [winsize [hostname]]");
        }

        UdpClient s;
        try
        {
            s = new UdpClient();
        }
        catch (SocketException)
        {
            Console.Error.WriteLine("socket creation failed");
            return;
        }

        s.Client.ReceiveTimeout = wumppkt.INITTIMEOUT;
        /*
            try {
                s.setSoTimeout(wumppkt.INITTIMEOUT);       // time in milliseconds
            } catch (SocketException) {
                Console.Error.WriteLine("socket exception: timeout not set!");
            }
        /* */

        // DNS lookup
        IPAddress dest;

        Console.Error.Write("Looking up address of " + desthost + "...");
        try
        {
            dest = Dns.GetHostAddresses(desthost)[0];
        }
        catch (SocketException)
        {
            Console.Error.WriteLine("host not found: " + desthost);
            return;
        }

        Console.Error.WriteLine(" got it: {0}", dest);

        // build REQ & send it
        wumppkt.REQ req = new wumppkt.REQ(wumppkt.BUMPPROTO, winsize, filename); // ctor for REQ

        Console.Error.WriteLine("req size = " + req.size() + ", filename=" + req.filename());

        IPEndPoint server = new IPEndPoint(dest, destport);

        try
        {
            s.Send(req.write(), req.size(), server);
        }
        catch (SocketException)
        {
            Console.Error.WriteLine("send() failed");
            return;
        }

        //============================================================

        // now receive the response
        IPEndPoint ANY, receivedEP;           // we don't set the address here!
        ANY = new IPEndPoint(IPAddress.Any, 0); // accept packet from any address

        int expected_block = 1;
        Stopwatch st = new Stopwatch();     // we will use Stopwatches to measure time
        st.Start();

        wumppkt.DATA data = new wumppkt.DATA();
        wumppkt.ACK ack = new wumppkt.ACK(0);

        int proto;        // for proto of incoming packets
        int opcode;
        int length;

        byte[] recvbuf; // for incoming packet

        //============================================================

        while (true)
        {
            //Thread.Sleep(500);
            try
            {
                receivedEP = new IPEndPoint(IPAddress.Any, 0);  // re-initialize before each use
                                                                //receivedEP = (IPEndPoint) ANY.MemberwiseClone();
                recvbuf = s.Receive(ref receivedEP);
            }
            catch (SocketException se)
            {
                // what do you do here??
                if (se.SocketErrorCode == SocketError.TimedOut)
                {
                    Console.Error.WriteLine("hard timeout");
                    continue;
                }
                else
                {
                    Console.Error.WriteLine("receive() failed");
                    return;
                }
            }

            proto = wumppkt.proto(recvbuf);
            opcode = wumppkt.opcode(recvbuf);
            length = recvbuf.Length;

            // The new packet might not actually be a DATA packet.
            // But we can still build one and see, provided:
            //   1. proto =   wumppkt.BUMPPROTO
            //   2. opcode =  wumppkt.DATAop
            //   3. length >= wumppkt.DHEADERSIZE

            if (proto == wumppkt.BUMPPROTO
            && opcode == wumppkt.DATAop
            && length >= wumppkt.DHEADERSIZE)
            {
                data = new wumppkt.DATA(recvbuf);
            }
            else if (proto == wumppkt.BUMPPROTO && opcode == wumppkt.ERRORop)
            {
                wumppkt.ERROR error = new wumppkt.ERROR(recvbuf);
                Console.Error.WriteLine("error packet: code={0}", error.errcode());
                data = null;
            }
            else
            {
                data = null;
            }

            long elapsedtime = st.ElapsedMilliseconds;

            // the following seven items we can print always
            Console.Error.Write("rec'd packet: len=" + length);
            Console.Error.Write("; proto=" + proto);
            Console.Error.Write("; opcode=" + opcode);
            Console.Error.Write("; src=({0}/{1})", receivedEP.Address, receivedEP.Port);
            Console.Error.Write("; time=" + elapsedtime);
            Console.Error.WriteLine();

            if (data == null)
            {
                Console.Error.WriteLine("         unknown packet");
            }
            else
            {
                Console.Error.WriteLine("         DATA packet blocknum = " + data.blocknum());
                string datastr = System.Text.Encoding.UTF8.GetString(data.data(), 0, data.size() - wumppkt.DHEADERSIZE);
                Console.Write(datastr);
                //System.out.write(data.data(), 0, data.size() - wumppkt.DHEADERSIZE);
            }
			// The following is for you to do:
			// check port, packet size, type, block, etc
			// latch on to port, if block == 1

			// send ack


			expected_block = data.blocknum();
			//sets expected_block to the block number of the block that just arrived
			ack = new wumppkt.ACK(wumppkt.BUMPPROTO, expected_block);
            expected_block++;
            // if it passes all the checks:
            //write data, increment expected_block
            // exit if data size is < 512

            Console.Error.WriteLine("\nsending ACK[{0}]", expected_block);
            try
            {
                s.Send(ack.write(), ack.size(), server);
            }
            catch (IOException)
            {
                Console.Error.WriteLine("send() failed");
                return;
            }
            st.Restart();

            // if it passes all the checks:
            //write data, increment expected_block
            // exit if data size is < 512

        } // while
          /* */
    }
}