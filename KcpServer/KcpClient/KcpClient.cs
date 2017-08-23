﻿
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using k = kcpwarpper;
using static kcpwarpper.KCP;
using System.Runtime.InteropServices;
namespace KcpClient
{
    public unsafe class KcpClient : UdpClient
    {
#if PRINTPACK
        void printpack(string s)
        {
            Console.WriteLine(s);
        }
#endif
        k.IKCPCB* kcp = null;
        public KcpClient(byte[] arr, int sid, byte[] appData) : base(arr, sid, appData)
        {

        }
        k.d_output realsend;
        void releaseKcp()
        {
            if (kcp != null)
            {
                Console.WriteLine("release codec");
                ikcp_release(kcp);
                kcp = null;
            }
        }
        void initKcp()
        {
            releaseKcp();
            var errno = ikcp_create((uint)this.SessionId, (void*)0);
            if ((int)errno == -1)
            {
                throw new NullReferenceException("init codec failed");
            }
            kcp = errno;
            realsend = new k.d_output(udp_output);
            kcp->output = Marshal.GetFunctionPointerForDelegate(realsend);
            /*该调用将会设置协议的最大发送窗口和最大接收窗口大小，默认为32. 这个可以理解为 TCP的 SND_BUF 和 RCV_BUF，只不过单位不一样 SND/RCV_BUF 单位是字节，这个单位是包。*/

            ikcp_wndsize(kcp, 128, 128);
            /*
            nodelay ：是否启用 nodelay模式，0不启用；1启用。
            interval ：协议内部工作的 interval，单位毫秒，比如 10ms或者 20ms
            resend ：快速重传模式，默认0关闭，可以设置2（2次ACK跨越将会直接重传）
            nc ：是否关闭流控，默认是0代表不关闭，1代表关闭。
            普通模式：`ikcp_nodelay(kcp, 0, 40, 0, 0);
            极速模式： ikcp_nodelay(kcp, 1, 10, 2, 1);
             */
            ikcp_nodelay(kcp, 1, 10, 2, 1);

            /*最大传输单元：纯算法协议并不负责探测 MTU，默认 mtu是1400字节，可以使用ikcp_setmtu来设置该值。该值将会影响数据包归并及分片时候的最大传输单元。*/
            ikcp_setmtu(kcp, Utilities.PackSettings.MAX_DATA_LEN);//可能还要浪费几个字节

            /*最小RTO：不管是 TCP还是 KCP计算 RTO时都有最小 RTO的限制，即便计算出来RTO为40ms，由于默认的 RTO是100ms，协议只有在100ms后才能检测到丢包，快速模式下为30ms，可以手动更改该值：*/
            kcp->rx_minrto = 100;

            Console.WriteLine("init codec");
        }
        /// <summary>
        /// real send
        /// </summary>
        /// <param name="buf"></param>
        /// <param name="len"></param>
        /// <param name="kcp"></param>
        /// <param name="user"></param>
        /// <returns></returns>

        int udp_output(byte* buf, int len, k.IKCPCB* kcp, void* user)
        {
            byte[] buff = new byte[len];
            Marshal.Copy(new IntPtr(buf), buff, 0, len);
#if PRINTPACK
            printpack($"kcp_output:{buff.Length}:{string.Join(",",buff)}");
#endif
            if (Outgoing != null)
            {
                Outgoing.Enqueue(buff);
            }
            return 0;
        }
        protected override void ProcessIncomingData(byte[] data)
        {
#if PRINTPACK
            printpack($"ikcp_input:{data.Length}:{string.Join(",", data)}");
#endif
            fixed (byte* p = &data[0])
            {
                ikcp_input(kcp, p, data.Length);
            }
        }

        protected override void ProcessOutgoingData(byte[] buff)
        {
            //give to kcp to determin how to send
            if (kcp == null)
            {
                throw new NullReferenceException();
            }
            fixed (byte* p = &buff[0])
            {
                var ret = ikcp_send(kcp, p, buff.Length);
            }
        }
        byte[] kb = new byte[Utilities.PackSettings.MAX_RECBUFF_LEN];
        protected override void AfterService()
        {
            if (kcp == null)
            {
                return;
            }
            ikcp_update(kcp, (uint)Environment.TickCount);
            fixed (byte* p = &kb[0])
            {
                int kcnt = 0;
                do
                {
                    kcnt = ikcp_recv(kcp, p, kb.Length);
                    if (kcnt > 0)
                    {
                        var data = new byte[kcnt];
                        Array.Copy(kb, data, kcnt);
                        if (Incoming != null)
                        {
                            Incoming.Enqueue(data);
                        }
                    }
                } while (kcnt > 0);
            }
        }
        protected override void OnHandShake()
        {
            base.OnHandShake();
            initKcp();
        }
        public override void Close()
        {
            releaseKcp();
            base.Close();
        }
    }
}
