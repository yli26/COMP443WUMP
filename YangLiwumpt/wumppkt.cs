using System;
using System.Diagnostics;

// The following implements the packet formats for the BUMP client.

// This C# version differs from the Java version in that there is no use of
// a base-class packet BASE.

// Outgoing packets are REQ, ACK and ERROR. Incoming packets are ERROR and DATA 
// Many implementations will never receive ERROR packets.

// Incoming packets have a constructor to convert the packet (byte[]) to the appropriate class.
// Before this is done, methods getProto() and getOpcode() can be applied to determine
// the packet type. 

// For outgoing packets there is a write() method that converts the class to byte[] format,
// suitable for sending.

// The "raw" packet format, as sent and received via DatagramSocket, is byte[]. 

/*

REQ
 +-------+------+----------+----------------------------+
 | proto |opcode| winsize  |  filename (NUL terminated) |
 | 1 byte|1 byte| 2 bytes  |  N+1 bytes                 |
 +-------+------+----------+----------------------------+

DATA
 +-------+------+----------+-------------+--------------------------+
 | proto |opcode| padding  |  blocknum   |    DATA                  |
 | 1 byte|1 byte| 2 bytes  |  4 bytes    |  DATASIZE bytes, or less |
 +-------+------+----------+-------------+--------------------------+

ACK
 +-------+------+----------+-------------+
 | proto |opcode| padding  |  blocknum   |
 | 1 byte|1 byte| 2 bytes  |  4 bytes    |
 +-------+------+----------+-------------+

ERROR
 +-------+------+----------+
 | proto |opcode| errcode  |
 | 1 byte|1 byte| 2 bytes  |
 +-------+------+----------+


*/

public class wumppkt {

    public static readonly short BUMPPROTO = 1;
    public static readonly short HUMPPROTO = 2;
    public static readonly short CHUMPPROTO= 3;

    public static readonly short REQop = 1;
    public static readonly short DATAop= 2;
    public static readonly short ACKop = 3;
    public static readonly short ERRORop=4;
    public static readonly short HANDOFFop=5;

    public static readonly short SERVERPORT = 4715;
    public static readonly short SAMEPORT   = 4517;

    public static readonly int   INITTIMEOUT = 3000;   // milliseconds
    public static readonly int   SHORTSIZE = 2;            // in bytes
    public static readonly int   INTSIZE   = 4;
    public static readonly int   BASESIZE  = 2;
    public static readonly int   MAXDATASIZE=512;
    public static readonly int   DHEADERSIZE = BASESIZE + SHORTSIZE + INTSIZE; // DATA header size
    public static readonly int   MAXSIZE  = DHEADERSIZE + MAXDATASIZE;

    public static readonly int   EBADPORT  =1;  /* packet from wrong port */
    public static readonly int   EBADPROTO =2;  /* unknown protocol */
    public static readonly int   EBADOPCODE=3;  /* unknown opcode */
    public static readonly int   ENOFILE   =4;  /* REQ for nonexistent file */
    public static readonly int   ENOPERM   =5;  /* REQ for file with wrong permissions */

public static int proto(byte[] buf) {
    return  buf[0];
}

public static int opcode(byte[] buf) {
    return buf[1];
}

public class BASE { 
// don't construct these unless the buffer has length >=4!

// the data:
    private byte  _proto;
    private byte  _opcode;

    //---------------------------------

    public BASE(int proto, int opcode) {
        _proto = (byte) proto;
        _opcode = (byte) opcode;
    }

    public BASE(byte[] buf) {        // constructs pkt out of packetbuf
    }

    public BASE() {}                 // packet ctors do all the work!

    public virtual byte[] write() {         // placeholder
        return null;
    }

    public virtual int size() {
        return BASESIZE;
    }

    public int proto()  {return _proto;}
    public int opcode() {return _opcode;}
}

/*******************
REQ
 +-------+------+----------+----------------------------+
 | proto |opcode| winsize  |  filename (NUL terminated) |
 | 1 byte|1 byte| 2 bytes  |  N+1 bytes                 |
 +-------+------+----------+----------------------------+
*/

public class REQ : BASE {

    private short  _winsize;
    private string _filename;

    //---------------------------------

    public REQ(int proto, int winsize, string filename) : base(proto, REQop) {
        _winsize = (short) winsize;
        _filename = filename;
    }

    public REQ(int winsize, string filename) : this(BUMPPROTO, winsize, filename) {
    }

    public override byte[] write() {
        byte[] buf = new byte[size()];
	buf[0] = (byte) proto();
	buf[1] = (byte) opcode();
	setShort(buf, BASESIZE, _winsize);
	for (int i = 0; i< _filename.Length; i++) {
		buf[i+BASESIZE+SHORTSIZE] = (byte) _filename[i];
	}
	return buf;
    }

    public override int size() {
        return base.size() + SHORTSIZE + _filename.Length + 1;
    }

    public string filename() {return _filename;}
}

/*******************
ACK
 +-------+------+----------+-------------+
 | proto |opcode| padding  |  blocknum   |
 | 1 byte|1 byte| 2 bytes  |  4 bytes    |
 +-------+------+----------+-------------+
*/
public class ACK : BASE {

    private int _blocknum;

    //---------------------------------

    public ACK(int blocknum) : this(BUMPPROTO, blocknum) {
    }

    public ACK(short proto, int blocknum) : base(proto, ACKop) {
        _blocknum = blocknum;
    }

    public int blocknum() {return _blocknum;}
    public void setblock(int blocknum) {_blocknum = blocknum;}

    public override byte[] write() {
        byte[] buf = new byte[size()];
	buf[0] = (byte) proto();
	buf[1] = (byte) opcode();
	setShort(buf, BASESIZE, 0);
	setInt(buf, BASESIZE+SHORTSIZE, _blocknum);
	return buf;
    }

    public override int size() {
        return base.size() + SHORTSIZE + INTSIZE;
    }

}

/* ******************
DATA
 +-------+------+----------+-------------+--------------------------+
 | proto |opcode| padding  |  blocknum   |    DATA                  |
 | 1 byte|1 byte| 2 bytes  |  4 bytes    |  DATASIZE bytes, or less |
 +-------+------+----------+-------------+--------------------------+
*/
public class DATA : BASE {

    private int _blocknum;
    private byte[] _data;

    //---------------------------------

    // this is not used
    public DATA(int proto, int blocknum, byte[] data) : base(proto, DATAop) {
        _blocknum = blocknum;
        _data = data;
    }

    public DATA(byte[] buf, int bufsize) : base(buf[0], buf[1]) {
	_blocknum = getInt(buf, BASESIZE + SHORTSIZE);
	Debug.Assert(bufsize >= DHEADERSIZE, "buffer is too short to convert to a DATA object");
	_data = new byte[bufsize - DHEADERSIZE];
	for (int i = 0; i< _data.Length; i++) _data[i] = buf[i+DHEADERSIZE];
    }

    public DATA(byte[] buf) : this(buf, buf.Length) {
    }

    // for building a DATA out of incoming buffer:
    public DATA(int proto, byte[] buf, int bufsize) : base(proto, DATAop) {
    }

    public DATA(int proto) : base(proto, DATAop) {        // for creating "empty" DATA objects
        _blocknum = 0;
        _data = new byte[MAXDATASIZE];
    }

    public DATA() : this(BUMPPROTO) { }

    public int blocknum() {return _blocknum;}
    public byte[] data() {return _data;}

    public override byte[] write() {     // not complete but not needed
	return null;
    }

    public override int size() {
        return base.size() + SHORTSIZE + INTSIZE + _data.Length;
    }
}

/******************
ERROR
 +-------+------+----------+
 | proto |opcode| errcode  |
 | 1 byte|1 byte| 2 bytes  |
 +-------+------+----------+
*/
public class ERROR : BASE {

    private short _errcode;

    //---------------------------------
    public ERROR(short proto, short errcode) : base(proto, ERRORop) {
		_errcode = errcode;
	}

    public short errcode() {return _errcode;}

    // for sending an ERROR packet
    public override byte[] write() {
        byte[] buf = new byte[size()];
	buf[0] = (byte) proto();
	buf[1] = (byte) opcode();
	setShort(buf, BASESIZE, _errcode);
	return buf;
    }

    // for receiving an ERROR packet
    public ERROR(byte[] buf) : base(buf[0], buf[1]) {
	_errcode = getShort(buf,BASESIZE);
    }

    public ERROR(int proto, byte[] buf) : base(proto, ERRORop) {}

    public override int size() {return base.size() + SHORTSIZE;}
}

 static void Main2(string[] args) {
 }

    public static short getShort(byte[] buf, int pos) {
   	//if (buf.Length < HSIZE) throw new IOException("buffer too short");
	Debug.Assert(buf.Length >= pos+2, "getShort: buffer too short");
	return (short) (((buf[pos]) << 8) | ((buf[pos+1]) & 0xff) );
    }

    static void setShort(byte[] buf, int pos, short val) {
	Debug.Assert(buf.Length >= pos+2, "setShort: buffer too short");
        buf[pos] = (byte) (val >> 8);
        buf[pos+1] = (byte) val;
    }

    // only works for 16-bit ints
    public static int getInt(byte[] buf, int pos) {
   	//if (buf.Length < HSIZE) throw new IOException("buffer too short");
	Debug.Assert(buf.Length >= pos+4, "getInt: buffer too short");
	return (short) (((buf[pos+2]) << 8) | ((buf[pos+3]) & 0xff) );
    }

    // only works for 16-bit ints
    static void setInt(byte[] buf, int pos, int val) {
	Debug.Assert(buf.Length >= pos+4, "setInt: buffer too short");
        buf[pos]   = 0; 
        buf[pos+1] = 0;
	buf[pos+2] = (byte) (val >> 8);
	buf[pos+3] = (byte) val;
    }

}
