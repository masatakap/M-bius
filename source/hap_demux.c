#include <libavformat/avformat.h>
#include <libavutil/display.h>
#include <stdlib.h>
#include <math.h>
typedef struct {AVFormatContext *f;AVPacket *cur,*next;int stream;double base,unit,duration;} Reader;
__declspec(dllexport) void hap_close(Reader *r){if(!r)return;av_packet_free(&r->cur);av_packet_free(&r->next);avformat_close_input(&r->f);free(r);}
__declspec(dllexport) Reader *hap_open(const char *path,int *w,int *h,double *duration,double *fps,double *aspect,int *rotation){
 Reader *r=calloc(1,sizeof(Reader)); if(!r)return NULL;
 if(avformat_open_input(&r->f,path,NULL,NULL)<0){hap_close(r);return NULL;}
 r->stream=-1;for(unsigned i=0;i<r->f->nb_streams;i++)if(r->f->streams[i]->codecpar->codec_id==AV_CODEC_ID_HAP){r->stream=i;break;}
 if(r->stream<0){hap_close(r);return NULL;}
 AVStream *s=r->f->streams[r->stream];*w=s->codecpar->width;*h=s->codecpar->height;
 if(*w<=0||*h<=0||*w>32768||*h>32768){hap_close(r);return NULL;}
 r->unit=av_q2d(s->time_base);r->base=s->start_time==AV_NOPTS_VALUE?0:s->start_time*r->unit;
 *duration=s->duration==AV_NOPTS_VALUE?(r->f->duration/(double)AV_TIME_BASE):s->duration*r->unit;r->duration=*duration;
 *fps=av_q2d(s->avg_frame_rate);if(!isfinite(*fps)||*fps<=0)*fps=av_q2d(s->r_frame_rate);if(!isfinite(*fps)||*fps<=0)*fps=30;
 AVRational sar=s->sample_aspect_ratio;if(sar.num<=0)sar=s->codecpar->sample_aspect_ratio;*aspect=sar.num>0?av_q2d(sar):1;
 const AVPacketSideData *sd=av_packet_side_data_get(s->codecpar->coded_side_data,s->codecpar->nb_coded_side_data,AV_PKT_DATA_DISPLAYMATRIX);
 *rotation=sd?(int)round(-av_display_rotation_get((const int32_t*)sd->data)):0;
 r->cur=av_packet_alloc();r->next=av_packet_alloc();return r;
}
static double pts(Reader*r,AVPacket*p){return (p->pts==AV_NOPTS_VALUE?p->dts:p->pts)*r->unit-r->base;}
static int next_packet(Reader*r){av_packet_unref(r->next);while(av_read_frame(r->f,r->next)>=0){if(r->next->stream_index==r->stream)return 1;av_packet_unref(r->next);}return 0;}
__declspec(dllexport) int hap_read(Reader*r,double t,unsigned char **data,double *stamp){
 if(!r)return -1;
 if(!r->cur->size||t+0.00001<pts(r,r->cur)||(r->next->size&&t>pts(r,r->next)+.5)){
   if(av_seek_frame(r->f,r->stream,(int64_t)((t+r->base)/r->unit),AVSEEK_FLAG_BACKWARD)<0)return -2;
   av_packet_unref(r->cur);av_packet_unref(r->next);next_packet(r);
 }
 while(r->next->size&&(!r->cur->size||pts(r,r->next)<=t+.000001)){av_packet_unref(r->cur);av_packet_move_ref(r->cur,r->next);next_packet(r);}
 if(!r->cur->size)return -3;*data=r->cur->data;*stamp=pts(r,r->cur);return r->cur->size;
}
