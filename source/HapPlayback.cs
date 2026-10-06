using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
namespace VideoMosaic;
internal sealed class HapPlayback : IDisposable
{
    [DllImport("hap_demux.dll",CallingConvention=CallingConvention.Cdecl)]static extern nint hap_open([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out int w,out int h,out double duration,out double fps,out double aspect,out int rotation);
    [DllImport("hap_demux.dll",CallingConvention=CallingConvention.Cdecl)]static extern int hap_read(nint reader,double seconds,out nint data,out double timestamp);
    [DllImport("hap_demux.dll",CallingConvention=CallingConvention.Cdecl)]static extern void hap_close(nint reader);
    internal record Plane(int Format,byte[] Data);
    internal record Frame(double Timestamp,Plane[] Planes);
    public readonly int Width,Height,Rotation;
    public readonly double Duration,Fps,Aspect;
    readonly nint reader;
    readonly object gate=new();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly AutoResetEvent wake=new(false);
    readonly Thread worker;
    double position,start;
    bool playing;
    volatile bool stopped;
    Frame? frame;
    public string? Error;
    public Frame? Latest=>Volatile.Read(ref frame);
    public double Position {get{lock(gate)return Current();}}
    double Current(){double p=position+(playing?clock.Elapsed.TotalSeconds-start:0);return Duration>0?((p%Duration)+Duration)%Duration:Math.Max(0,p);}
    public void Play(bool play){lock(gate){position=Current();start=clock.Elapsed.TotalSeconds;playing=play;}wake.Set();}
    public void Seek(double target){lock(gate){position=Math.Clamp(target,0,Math.Max(0,Duration-1/Fps));start=clock.Elapsed.TotalSeconds;}wake.Set();}
    public static HapPlayback? Open(string path){var r=hap_open(path,out var w,out var h,out var d,out var f,out var a,out var rot);return r==0?null:new(r,w,h,d,f,a,rot);}
    HapPlayback(nint r,int w,int h,double d,double f,double a,int rot){reader=r;Width=w;Height=h;Duration=d;Fps=f;Aspect=a;Rotation=((rot%360)+360)%360;worker=new Thread(Run){IsBackground=true,Name="Hap packet reader"};worker.Start();}
    void Run()
    {
        try{while(!stopped&&Error==null){double target=Position;int size=hap_read(reader,target,out var ptr,out var stamp);if(size<0)throw new InvalidDataException("Cannot read Hap frame: "+size);if(Latest?.Timestamp!=stamp){if(size>512*1024*1024)throw new InvalidDataException("Hap frame too large");var data=new byte[size];Marshal.Copy(ptr,data,0,size);var planes=Decode(data);Volatile.Write(ref frame,new(stamp,planes));}bool active;lock(gate)active=playing;wake.WaitOne(active?Math.Max(1,(int)(250/Fps)):1000);}}
        catch(Exception ex){Error=ex.Message;}
        finally{hap_close(reader);}
    }
    internal static Plane[] Decode(byte[] data)
    {
        var top=Section(data,0);if(top.Type==0x0d){var list=new List<Plane>();for(int i=top.Start;i<top.End;){var part=Section(data,i);list.Add(DecodePlane(data,part));i=part.End;}if(list.Count<1||list.Count>2)throw new InvalidDataException("Invalid Hap planes");if(list.Count==2){if(list[0].Format==1&&list[1].Format==15)list.Reverse();if(list[0].Format!=15||list[1].Format!=1)throw new InvalidDataException("Invalid Hap plane combination");}return list.ToArray();}return [DecodePlane(data,top)];
    }
    readonly record struct Part(int Type,int Start,int End);
    static Part Section(byte[] data,int offset){if(offset<0||offset>data.Length-4)throw new InvalidDataException("Invalid Hap header");int size=data[offset]|data[offset+1]<<8|data[offset+2]<<16,type=data[offset+3],header=4;if(size==0){if(offset>data.Length-8)throw new InvalidDataException("Invalid Hap header");size=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset+4)));header=8;}int end=checked(offset+header+size);if(end>data.Length)throw new InvalidDataException("Truncated Hap frame");return new(type,offset+header,end);}
    static Plane DecodePlane(byte[] data,Part part)
    {
        int format=part.Type&15,compression=part.Type>>4;
        if(format is not (1 or 2 or 3 or 11 or 12 or 14 or 15))throw new InvalidDataException("Unsupported Hap texture format");
        if(compression==10)return new(format,data[part.Start..part.End]);
        if(compression==11)return new(format,Snappy(data.AsSpan(part.Start,part.End-part.Start)));
        if(compression!=12)throw new InvalidDataException("Unsupported Hap compression");
        var instructions=Section(data,part.Start);if(instructions.Type!=1||instructions.End>part.End)throw new InvalidDataException("Invalid Hap instructions");
        byte[]? compressors=null;int[]? sizes=null,offsets=null;
        for(int i=instructions.Start;i<instructions.End;){var section=Section(data,i);if(section.End>instructions.End)throw new InvalidDataException("Invalid Hap table");if(section.Type==2)compressors=data[section.Start..section.End];if(section.Type is 3 or 4){if((section.End-section.Start)%4!=0)throw new InvalidDataException("Invalid Hap table size");var values=new int[(section.End-section.Start)/4];for(int j=0;j<values.Length;j++)values[j]=checked((int)BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(section.Start+j*4)));if(section.Type==3)sizes=values;else offsets=values;}i=section.End;}
        if(compressors==null||sizes==null||sizes.Length!=compressors.Length||sizes.Length>4096||offsets!=null&&offsets.Length!=sizes.Length)throw new InvalidDataException("Invalid Hap chunks");
        var starts=new int[sizes.Length];int cursor=instructions.End;for(int i=0;i<sizes.Length;i++){starts[i]=offsets==null?cursor:checked(instructions.End+offsets[i]);cursor=checked(starts[i]+sizes[i]);if(starts[i]<instructions.End||cursor>part.End)throw new InvalidDataException("Truncated Hap chunk");}
        var chunks=new byte[sizes.Length][];
        Parallel.For(0,chunks.Length,new ParallelOptions{MaxDegreeOfParallelism=2},i=>{var span=data.AsSpan(starts[i],sizes[i]);chunks[i]=compressors[i] switch{10=>span.ToArray(),11=>Snappy(span),_=>throw new InvalidDataException("Invalid Hap chunk compressor")};});
        int length=checked(chunks.Sum(c=>c.Length));if(length>512*1024*1024)throw new InvalidDataException("Hap texture too large");var result=new byte[length];cursor=0;foreach(var chunk in chunks){chunk.CopyTo(result,cursor);cursor+=chunk.Length;}return new(format,result);
    }
    static byte[] Snappy(ReadOnlySpan<byte> input)
    {
        int p=0,shift=0;uint size=0;while(true){if(p>=input.Length||shift>28)throw new InvalidDataException("Invalid Snappy size");byte b=input[p++];size|=(uint)(b&127)<<shift;if((b&128)==0)break;shift+=7;}if(size>512*1024*1024)throw new InvalidDataException("Snappy output too large");var output=new byte[size];int dst=0;
        while(p<input.Length&&dst<output.Length){int tag=input[p++],kind=tag&3,length,offset=0;if(kind==0){length=tag>>2;if(length<60)length++;else{int bytes=length-59;if(p+bytes>input.Length)throw new InvalidDataException("Snappy literal header");uint raw=0;for(int j=0;j<bytes;j++)raw|=(uint)input[p++]<<(j*8);length=checked((int)raw+1);}if(length>input.Length-p||length>output.Length-dst)throw new InvalidDataException("Snappy literal bounds");input.Slice(p,length).CopyTo(output.AsSpan(dst));p+=length;dst+=length;}else{length=kind==1?4+((tag>>2)&7):1+(tag>>2);int bytes=kind==1?1:kind==2?2:4;if(p+bytes>input.Length)throw new InvalidDataException("Snappy copy header");uint raw=kind==1?(uint)(tag&224)<<3:0;for(int j=0;j<bytes;j++)raw|=(uint)input[p++]<<(j*8);offset=checked((int)raw);if(offset<=0||offset>dst||length>output.Length-dst)throw new InvalidDataException("Snappy copy bounds");for(int j=0;j<length;j++){output[dst]=output[dst-offset];dst++;}}}
        if(dst!=output.Length||p!=input.Length)throw new InvalidDataException("Snappy size mismatch");return output;
    }
    public void RequestStop(){stopped=true;wake.Set();}
    public void Dispose(){RequestStop();worker.Join();wake.Dispose();}
}



