using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

// Reads the uncompressed payload of the specific UnityFS format used by the
// supported game build. The installer checks the original file's SHA-256 first.
public static class AeonUnityPayload
{
    sealed class Block { public int Raw; public int Packed; public int Flags; }
    static byte[] Bytes(BinaryReader r,int count) { byte[] b=r.ReadBytes(count);if(b.Length!=count)throw new EndOfStreamException();return b; }
    static uint U32(BinaryReader r) { byte[] b=Bytes(r,4);return ((uint)b[0]<<24)|((uint)b[1]<<16)|((uint)b[2]<<8)|b[3]; }
    static long I64(BinaryReader r) { return unchecked((long)(((ulong)U32(r)<<32)|U32(r))); }
    static int U16(BinaryReader r) { byte[] b=Bytes(r,2);return (b[0]<<8)|b[1]; }
    static string CString(BinaryReader r) { var b=new List<byte>();byte c;while((c=r.ReadByte())!=0){if(b.Count>256)throw new InvalidDataException("Unity header string too long.");b.Add(c);}return Encoding.UTF8.GetString(b.ToArray()); }
    static int Size(uint n,int maximum) {if(n>maximum)throw new InvalidDataException("Unity block too large.");return (int)n;}
    static byte[] Decode(byte[] source,int length,int flags)
    {
        int compression=flags&63;
        if(compression==0){if(source.Length!=length)throw new InvalidDataException("Invalid raw block.");return source;}
        if(compression!=2 && compression!=3)throw new InvalidDataException("Unsupported Unity compression.");
        byte[] output=new byte[length];int p=0,q=0;
        while(p<source.Length)
        {
            int token=source[p++],literal=token>>4;
            if(literal==15){int n;do{if(p>=source.Length)throw new InvalidDataException("Truncated LZ4 literal.");n=source[p++];literal=checked(literal+n);}while(n==255);}
            if(literal>source.Length-p || literal>length-q)throw new InvalidDataException("LZ4 literal exceeds block.");
            Buffer.BlockCopy(source,p,output,q,literal);p+=literal;q+=literal;
            if(p==source.Length)break;
            if(p+2>source.Length)throw new InvalidDataException("Truncated LZ4 offset.");
            int offset=source[p]|(source[p+1]<<8);p+=2;
            if(offset==0 || offset>q)throw new InvalidDataException("Invalid LZ4 offset.");
            int match=(token&15)+4;
            if((token&15)==15){int n;do{if(p>=source.Length)throw new InvalidDataException("Truncated LZ4 match.");n=source[p++];match=checked(match+n);}while(n==255);}
            if(match>length-q)throw new InvalidDataException("LZ4 match exceeds block.");
            for(int i=0;i<match;i++)output[q+i]=output[q+i-offset];q+=match;
        }
        if(q!=length)throw new InvalidDataException("Incomplete Unity block.");
        return output;
    }
    public static void Extract(string original,string output)
    {
        using(var stream=File.OpenRead(original))
        using(var reader=new BinaryReader(stream))
        {
            if(CString(reader)!="UnityFS")throw new InvalidDataException("Not a UnityFS bundle.");
            uint version=U32(reader);CString(reader);CString(reader);
            long size=I64(reader);
            int packedInfo=Size(U32(reader),8*1024*1024),rawInfo=Size(U32(reader),8*1024*1024);
            int flags=unchecked((int)U32(reader));
            if(size!=stream.Length || version<7 || version>8)throw new InvalidDataException("Unsupported UnityFS header.");
            stream.Position=(stream.Position+15)&~15L;
            long dataStart=stream.Position;
            if((flags&128)!=0)stream.Position=stream.Length-packedInfo;
            byte[] info=Decode(Bytes(reader,packedInfo),rawInfo,flags);
            if((flags&128)!=0)stream.Position=dataStart;
            if((flags&512)!=0)stream.Position=(stream.Position+15)&~15L;
            var blocks=new List<Block>();long total=0;
            using(var mr=new BinaryReader(new MemoryStream(info)))
            {
                Bytes(mr,16);int count=Size(U32(mr),16384);
                for(int i=0;i<count;i++)
                {
                    var block=new Block();block.Raw=Size(U32(mr),64*1024*1024);block.Packed=Size(U32(mr),64*1024*1024);block.Flags=U16(mr);
                    total+=block.Raw;if(total>512L*1024*1024)throw new InvalidDataException("Unity payload too large.");blocks.Add(block);
                }
            }
            using(var target=new FileStream(output,FileMode.Create,FileAccess.Write,FileShare.None))
            foreach(var block in blocks)
            {
                byte[] data=Decode(Bytes(reader,block.Packed),block.Raw,block.Flags);
                target.Write(data,0,data.Length);
            }
        }
    }
}
