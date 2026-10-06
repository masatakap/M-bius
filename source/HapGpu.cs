using System.Runtime.InteropServices;
namespace VideoMosaic;
internal sealed partial class Gl
{
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Compressed(uint target,int level,uint format,int w,int h,int border,int size,nint data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void CompressedSub(uint target,int level,int x,int y,int w,int h,uint format,int size,nint data);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint CreateShader(uint type);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void ShaderSource(uint id,int count,nint strings,nint lengths);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void ShaderAction(uint id);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate uint CreateProgram();
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void AttachShader(uint program,uint shader);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void GetStatus(uint id,uint name,out int result);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate int GetUniform(uint id,[MarshalAs(UnmanagedType.LPStr)]string name);
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]delegate void Uniform(int location,int value);
    [DllImport("opengl32.dll")]static extern uint glGetError();
    [DllImport("opengl32.dll")]static extern void glReadPixels(int x,int y,int width,int height,uint format,uint type,byte[] output);
    public object VerifyHapGpu()
    {
        var results=new List<object>();uint output=Texture(4,4);GenFramebuffers(1,out uint fbo);BindFramebuffer(0x8D40,fbo);FramebufferTexture(0x8D40,0x8CE0,0x0DE1,output,0);
        try
        {
            byte[] red=[0,248,0,0,0,0,0,0];byte[] alpha=[128,128,0,0,0,0,0,0];byte[] q=[128,128,0,0,0,0,0,0,16,132,16,132,0,0,0,0];
            void Test(string name,HapPlayback.Plane plane,HapPlayback.Plane? second,int r,int g,int b)
            {
                uint tex=HapTexture(plane,4,4),a=second==null?0:HapTexture(second,4,4);
                try{BeginCanvas(4,4);BindFramebuffer(0x8D40,fbo);glClearColor(0,0,0,1);glClear(0x4000);HapImage(tex,a,plane.Format,0,new(0,0,4,4));byte[] pixels=new byte[4];glReadPixels(2,2,1,1,0x1908,0x1401,pixels);bool valid=Math.Abs(pixels[0]-r)<=4&&Math.Abs(pixels[1]-g)<=4&&Math.Abs(pixels[2]-b)<=4;results.Add(new{name,valid,pixels});if(!valid)throw new Exception("Hap GPU color test failed: "+name+" "+string.Join(",",pixels));}
                finally{glDeleteTextures(1,ref tex);if(a!=0)glDeleteTextures(1,ref a);}
            }
            Test("BC1 red",new(11,red),null,255,0,0);
            Test("BC3 alpha",new(14,[..alpha,..red]),null,128,0,0);
            Test("BC4 alpha",new(1,alpha),null,128,128,128);
            Test("Hap Q YCoCg",new(15,q),null,128,128,128);
            Test("Hap Q Alpha",new(15,q),new(1,[64,64,0,0,0,0,0,0]),32,32,32);
            foreach(int format in new[]{12,2,3}){uint tex=HapTexture(new(format,new byte[16]),4,4);glDeleteTextures(1,ref tex);results.Add(new{name="GPU format "+format,valid=true});}
        }
        finally{BindFramebuffer(0x8D40,0);DeleteFramebuffers(1,ref fbo);glDeleteTextures(1,ref output);}
        return results;
    }
    uint hapProgram;
    ShaderAction? useProgram;
    Uniform? uniform;
    GetUniform? getUniform;
    ShaderAction? activeTexture;
    public unsafe uint HapTexture(HapPlayback.Plane plane,int width,int height,uint existing=0)
    {
        uint format=plane.Format switch{11=>0x83F0,14 or 15=>0x83F3,1=>0x8DBB,12=>0x8E8C,2=>0x8E8F,3=>0x8E8E,_=>throw new InvalidDataException("Unknown Hap texture")};
        int expected=checked(((width+3)/4)*((height+3)/4)*(plane.Format is 1 or 11?8:16));if(plane.Data.Length!=expected)throw new InvalidDataException("Hap texture size mismatch");
        var upload=Load<Compressed>("glCompressedTexImage2D");while(glGetError()!=0){}uint texture=existing;if(texture==0)glGenTextures(1,out texture);glBindTexture(0x0DE1,texture);glTexParameteri(0x0DE1,0x2801,0x2601);glTexParameteri(0x0DE1,0x2800,0x2601);glTexParameteri(0x0DE1,0x2802,0x812F);glTexParameteri(0x0DE1,0x2803,0x812F);
        fixed(byte* p=plane.Data){if(existing==0)upload(0x0DE1,0,format,width,height,0,expected,(nint)p);else Load<CompressedSub>("glCompressedTexSubImage2D")(0x0DE1,0,0,0,width,height,format,expected,(nint)p);}uint error=glGetError();glBindTexture(0x0DE1,0);if(error!=0){if(existing==0)glDeleteTextures(1,ref texture);throw new Exception("GPU does not support this Hap texture: "+error);}return texture;
    }
    unsafe void InitHapShader()
    {
        if(hapProgram!=0)return;
        string source="""
        #version 120
        uniform sampler2D colorTex;
        uniform sampler2D alphaTex;
        uniform int mode;
        uniform int separateAlpha;
        uniform int rotation;
        void main(){
          vec2 uv=gl_TexCoord[0].st;
          if(rotation==90)uv=vec2(uv.y,1.0-uv.x);
          else if(rotation==180)uv=vec2(1.0-uv.x,1.0-uv.y);
          else if(rotation==270)uv=vec2(1.0-uv.y,uv.x);
          vec4 c=texture2D(colorTex,uv);
          if(mode==1){float scale=c.b*(255.0/8.0)+1.0;float co=(c.r-128.0/255.0)/scale;float cg=(c.g-128.0/255.0)/scale;float y=c.a;c=vec4(y+co-cg,y+cg,y-co-cg,1.0);}
          if(mode==2)c=vec4(1.0,1.0,1.0,c.r);
          if(mode==3)c=vec4(pow(max(c.rgb,vec3(0.0))/(vec3(1.0)+max(c.rgb,vec3(0.0))),vec3(1.0/2.2)),1.0);
          if(separateAlpha==1)c.a=texture2D(alphaTex,uv).r;
          gl_FragColor=c;
        }
        """;
        uint shader=Load<CreateShader>("glCreateShader")(0x8B30);nint text=Marshal.StringToHGlobalAnsi(source);try{Load<ShaderSource>("glShaderSource")(shader,1,(nint)(&text),0);}finally{Marshal.FreeHGlobal(text);}Load<ShaderAction>("glCompileShader")(shader);Load<GetStatus>("glGetShaderiv")(shader,0x8B81,out var ok);if(ok==0)throw new Exception("Hap GPU shader compile failed");hapProgram=Load<CreateProgram>("glCreateProgram")();Load<AttachShader>("glAttachShader")(hapProgram,shader);Load<ShaderAction>("glLinkProgram")(hapProgram);Load<GetStatus>("glGetProgramiv")(hapProgram,0x8B82,out ok);Load<ShaderAction>("glDeleteShader")(shader);if(ok==0)throw new Exception("Hap GPU shader link failed");useProgram=Load<ShaderAction>("glUseProgram");uniform=Load<Uniform>("glUniform1i");getUniform=Load<GetUniform>("glGetUniformLocation");activeTexture=Load<ShaderAction>("glActiveTexture");
    }
    public void HapImage(uint texture,uint alpha,int format,int rotation,RectangleF rect)
    {
        InitHapShader();useProgram!(hapProgram);uniform!(getUniform!(hapProgram,"colorTex"),0);uniform(getUniform(hapProgram,"alphaTex"),1);uniform(getUniform(hapProgram,"mode"),format==15?1:format==1?2:format is 2 or 3?3:0);uniform(getUniform(hapProgram,"rotation"),rotation);uniform(getUniform(hapProgram,"separateAlpha"),alpha!=0?1:0);
        activeTexture!(0x84C1);glBindTexture(0x0DE1,alpha);activeTexture(0x84C0);glEnable(0x0BE2);glBlendFunc(0x0302,0x0303);Image(texture,rect,true);glDisable(0x0BE2);activeTexture(0x84C1);glBindTexture(0x0DE1,0);activeTexture(0x84C0);useProgram(0);
    }
}




