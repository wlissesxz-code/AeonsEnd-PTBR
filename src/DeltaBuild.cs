using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

public static class AeonDeltaBuilder
{
    struct Chunk { public int Offset; public int Count; public Chunk(int p,int n){Offset=p;Count=n;} }
    static readonly ulong[] gear = MakeGear();
    static ulong[] MakeGear()
    {
        var table=new ulong[256]; ulong state=0x5A74616E6F6E7345UL;
        for(int i=0;i<table.Length;i++) { state^=state<<13;state^=state>>7;state^=state<<17;table[i]=state; }
        return table;
    }
    static IEnumerable<Chunk> Chunks(byte[] bytes)
    {
        int start=0,count=0; ulong hash=0;
        for(int i=0;i<bytes.Length;i++)
        {
            hash=unchecked((hash<<1)+gear[bytes[i]]);count++;
            if(count<2048 || ((hash&8191)!=0 && count<65536)) continue;
            yield return new Chunk(start,count); start=i+1;count=0;hash=0;
        }
        if(count>0) yield return new Chunk(start,count);
    }
    static string Key(SHA256 sha,byte[] data,Chunk c) { return c.Count+":"+Convert.ToBase64String(sha.ComputeHash(data,c.Offset,c.Count)); }
    static void Emit(BinaryWriter writer,byte[] target,int kind,int offset,int count)
    {
        if(count==0) return;
        writer.Write((byte)kind);
        if(kind==1) writer.Write((long)offset);
        writer.Write(count);
        if(kind==2) writer.Write(target,offset,count);
    }
    public static string Build(string original,string translated,string patch)
    {
        byte[] source=File.ReadAllBytes(original),target=File.ReadAllBytes(translated);
        var index=new Dictionary<string,Chunk>(); long copied=0,literals=0;
        using(var sha=SHA256.Create())
        {
            foreach(var c in Chunks(source)) { string key=Key(sha,source,c); if(!index.ContainsKey(key)) index.Add(key,c); }
            using(var file=new FileStream(patch,FileMode.Create,FileAccess.Write,FileShare.None))
            using(var gzip=new GZipStream(file,CompressionLevel.Optimal))
            using(var writer=new BinaryWriter(gzip,Encoding.UTF8))
            {
                writer.Write(Encoding.ASCII.GetBytes("AEPATCH1"));writer.Write((long)target.Length);
                int pendingKind=0,pendingOffset=0,pendingCount=0;
                foreach(var c in Chunks(target))
                {
                    Chunk match;
                    bool same=index.TryGetValue(Key(sha,target,c),out match);
                    int kind=same?1:2,offset=same?match.Offset:c.Offset;
                    if(same)copied+=c.Count;else literals+=c.Count;
                    if(kind==pendingKind && offset==pendingOffset+pendingCount) pendingCount+=c.Count;
                    else { Emit(writer,target,pendingKind,pendingOffset,pendingCount);pendingKind=kind;pendingOffset=offset;pendingCount=c.Count; }
                }
                Emit(writer,target,pendingKind,pendingOffset,pendingCount);writer.Write((byte)0);
            }
        }
        return "Copied="+copied+"; new="+literals+"; patch="+new FileInfo(patch).Length;
    }
}

