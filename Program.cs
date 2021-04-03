using System;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Common;
using DotNetty.Common.Utilities;
using DotNetty.Handlers.Logging;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;

namespace ConsoleApp4
{
    class Program
    {
        static void Main(string[] args) => RunAsync().Wait();

        static async Task RunAsync() => await Task.WhenAll(RunServerAsync(), RunClientAsync());

        static async Task RunServerAsync()
        {
            var serverGroup = new MultithreadEventLoopGroup(3);
            var workerGroup = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new ServerBootstrap();

                bootstrap.Group(serverGroup, workerGroup)
                    .Channel<TcpServerSocketChannel>()
                    .Option(ChannelOption.SoBacklog, 100)
                    .Handler(new LoggingHandler())
                    .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast(new MyEncodingHandler(), new MsgModelHandler(), new ServerHandler());
                    }));
                await bootstrap.BindAsync(IPAddress.Loopback, 8007);
                await Task.Delay(Timeout.Infinite);
            }
            finally
            {
                await Task.WhenAll(serverGroup.ShutdownGracefullyAsync(), workerGroup.ShutdownGracefullyAsync());
            }
        }

        static async Task RunClientAsync()
        {
            var group = new MultithreadEventLoopGroup();
            try
            {
                var bootstrap = new Bootstrap();

                bootstrap.Group(group)
                    .Channel<TcpSocketChannel>()
                    .Option(ChannelOption.TcpNodelay, true)
                    .Handler(new ActionChannelInitializer<ISocketChannel>(x =>
                    {
                        IChannelPipeline pipeline = x.Pipeline;
                        pipeline.AddLast(new MyEncodingHandler(), new MsgModelHandler(), new ClientHandler());
                    }));
                var channel = await bootstrap.ConnectAsync(IPAddress.Loopback, 8007);
                while (true)
                {
                    var msg = Console.ReadLine() ?? String.Empty;
                    if (msg.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    
                    await channel.Pipeline.WriteAndFlushAsync(new MsgModel(){Msg = msg,Time = DateTimeOffset.Now});
                }
            }
            finally
            {
                await Task.WhenAll(group.ShutdownGracefullyAsync());
            }
        }
    }

    public class ServerHandler : SimpleChannelInboundHandler<MsgModel>
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, MsgModel msg)
        {
            Console.WriteLine($"server:{msg.Msg}");
        }
    }

    public class ClientHandler : SimpleChannelInboundHandler<MsgModel>
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, MsgModel msg)
        {
           
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            
            Console.WriteLine($"client:{((MsgModel)message).Msg}");
            return base.WriteAsync(context, message);
        }
    }

    public class MsgModelHandler : SimpleChannelInboundHandler<string>
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, string msg)
        {
            var model = JsonSerializer.Deserialize<MsgModel>(msg);
            ctx.FireChannelRead(model);
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            return base.WriteAsync(context, JsonSerializer.Serialize(message));
        }
    }

    public class MyEncodingHandler : SimpleChannelInboundHandler<IByteBuffer>
    {
        protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
        {
            var model = ByteBufferUtil.DecodeString(msg, 0, msg.ReadableBytes, Encoding.UTF8);
            ctx.FireChannelRead(model);
        }

        public override Task WriteAsync(IChannelHandlerContext ctx, object message)
        {
            return ctx.WriteAsync(ByteBufferUtil.EncodeString(ctx.Allocator, message.ToString(), Encoding.UTF8));
        }
    }

    public class MsgModel
    {
        public string Msg { get; set; }

        public DateTimeOffset Time { get; set; }
    }
}
