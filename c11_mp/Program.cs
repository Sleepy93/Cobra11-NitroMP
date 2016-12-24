using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Drawing;
using System.Windows.Forms;
using System.Reflection;
using System.Runtime.InteropServices;

namespace c11_mp
{
    class Form1 : Form
    {
        public const string game_exe = "C11_PC.exe";
        const string lastip_file = "c11_mp.txt";
        const int PORT = 7777;
        //Client recieve
        const int BUFFER_SIZE = BUFFER_LEN + 1;
        const int BUFFER_LEN = 256;
        //Client send
        const int THD_SLEEP = 40; //25 FPS
        //Player, network car
        uint PLAYER_ADDR = 0x00AD4630;
        uint NET_ADDR = 0x00AD7EC0;
        const uint CAR_OFFSET = 0x3890;
        //
        TextBox tb_ip;
        Button bt_connect;
        //
        StreamReader sr;
        StreamWriter sw;
        //
        TcpClient client;
        Socket sock;
        Thread thd, thd2;
        MemoryEdit.Memory mem;
        Process game;
        byte id;

        //const uint movement_addr1 = 0x0049ED86,
        //    movement_addr2 = 0x0049EE1B;
        //const uint rotation_addr = 0x00422F75;

        //const uint offs_call = 9;
        //const uint offs_call2 = 16;

        //Function call
        /*Movement not working properly
        static byte[] movement_ovrd =
        {
            0x81, 0xFB, 0x28, 0x46, 0xAD, 0x00, //cmp ebx,00AD4628 - player
            0x75, 0x07,                         //jne +7
            0xE8, 0x00, 0x00, 0x00, 0x00,       //call dynamic address
            0xEB, 0x05,                         //jmp +5
            0xE8, 0x00, 0x00, 0x00, 0x00,       //call dynamic address
            0x90, 0x90                          //nop
        };

        static byte[] movement_injection =
        {
            0xD8, 0x43, 0x08,           //fadd dword ptr [ebx+08] - X
            0xD9, 0x5B, 0x08,           //fstp dword ptr [ebx+08]
            0xD8, 0x43, 0x0C,           //fadd dword ptr [ebx+0C] - Y
            0xD9, 0x5B, 0x0C,           //fstp dword ptr [ebx+0C]
            0xD9, 0x44, 0x24, 0x20,     //fld dword ptr [esp+20]
            0xD8, 0x43, 0x10,           //fadd dword ptr [ebx+10] - Z
            0xD9, 0x5B, 0x10,           //fstp dword ptr [ebx+10]
            0xC3                        //ret
        };

        static byte[] movement2_injection =
        {
            0xD9, 0x5B, 0x08,           //fstp dword ptr [ebx+08]
            0xD9, 0x5B, 0x0C,           //fstp dword ptr [ebx+0C]
            0xD9, 0x44, 0x24, 0x20,     //fld dword ptr [esp+20]
            0xD9, 0x5B, 0x10,           //fstp dword ptr [ebx+10]
            0xC3                        //ret
        };
        
        //Function call
        static byte[] rotation_ovrd =
        {
            0x81, 0xFB, 0x28, 0x46, 0xAD, 0x00, //cmp ebx,00AD4628 - player
            0x75, 0x0A,                         //jne +10
            0xE8, 0x00, 0x00, 0x00, 0x00,       //call dynamic address
            0x90, 0x90, 0x90, 0x90, 0x90        //nop
        };

        static byte[] rotation_injection = 
        {
            0x8B, 0x50, 0x04,           //mov edx,[eax+04] - X
            0x89, 0x51, 0x04,           //mov [ecx+04],edx
            0x8B, 0x50, 0x08,           //mov edx,[eax+08] - Y
            0x89, 0x51, 0x08,           //mov [ecx+08],edx
            0x8B, 0x40, 0x0C,           //mov eax,[eax+0C] - Z
            0x89, 0x41, 0x0C,           //mov [ecx+0C],eax
            0xC3                        //ret
        };
        */

        //Respawn disable for opponents
        const uint respawn_addr = 0x00560820, //injection address
            respawn2_addr = 0x00560829, //return address
            respawn3_addr = 0x00560BD3; //skip address
        const uint offs_jmp0 = 1,
            offs_jmp1 = 8,
            offs_jmp2 = 17,
            offs_jmp3 = 22;

        static byte[] respawn_ovrd =
        {
            0xE9, 0x00, 0x00, 0x00, 0x00,       //jmp dynamic address
            0x90, 0x90, 0x90, 0x90              //nop
        };

        static byte[] respawn_injection =
        {
            0x81, 0xFF, 0x0B, 0x3E, 0xAD, 0x00, //cmp edi,00AD3E0B - player
            0x0F, 0x85, 0x00, 0x00, 0x00, 0x00, //jne 00560BD3
            0xF6, 0xC4, 0x41,                   //test ah,41
            0x0F, 0x85, 0x00, 0x00, 0x00, 0x00, //jne 00560BD3
            0xE9, 0x00, 0x00, 0x00, 0x00        //jmp 00560829
        };

        public Form1()
        {
            Icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
            Text = "Cobra 11 Multiplayer Client 1.2 Alpha";
            ClientSize = new Size(320, 48);
            FormBorderStyle = FormBorderStyle.FixedSingle;

            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            tb_ip = new TextBox();
            tb_ip.Bounds = new Rectangle(12, 12, 128, 24);
            tb_ip.MaxLength = 15;
            if (File.Exists(lastip_file))
            {
                sr = new StreamReader(lastip_file);
                tb_ip.Text = sr.ReadLine();
                sr.Close();
            }
            Controls.Add(tb_ip);
            bt_connect = new Button();
            bt_connect.Text = "Connect";
            bt_connect.Bounds = new Rectangle(ClientRectangle.Right - 140, 12, 128, 24);
            bt_connect.Click += bt_connect_Click;
            Controls.Add(bt_connect);
        }

        protected override void OnClosed(EventArgs e)
        {
            if (game != null && !game.HasExited)
                game.Kill();
            Environment.Exit(0);
            base.OnClosed(e);
        }

        void bt_connect_Click(object sender, EventArgs e)
        {
            bt_connect.Enabled = false;
            tb_ip.Enabled = false;
            try
            {
                byte[] buffer = new byte[1];
                client = new TcpClient(tb_ip.Text, PORT);
                client.Client.Receive(buffer);
                id = buffer[0];
                sw = new StreamWriter(lastip_file, false, Encoding.Default);
                sw.Write(tb_ip.Text);
                sw.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Source + " - " + ex.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                bt_connect.Enabled = true;
                tb_ip.Enabled = true;
                return;
            }
            game = Process.Start(game_exe);
            mem = new MemoryEdit.Memory();
            while (!mem.Attach(game, 0x001F0FFF)) ;
            /*Movement not working properly
            //Code injection
            IntPtr tmp = mem.Allocate((uint)(movement_injection.Length + rotation_injection.Length));
            mem.WriteByte((uint)tmp, movement_injection, movement_injection.Length);
            mem.WriteByte((uint)((uint)tmp + movement_injection.Length), movement2_injection, movement2_injection.Length);
            mem.WriteByte((uint)((uint)tmp + movement_injection.Length + movement2_injection.Length), rotation_injection, rotation_injection.Length);
            //Override movement
            mem.WriteByte(movement_addr1, movement_ovrd, movement_ovrd.Length);
            mem.WriteByte(movement_addr2, movement_ovrd, movement_ovrd.Length);
            //Modify call
            mem.WriteByte(movement_addr1 + offs_call, BitConverter.GetBytes(((uint)tmp - (movement_addr1 + offs_call + 4))), 4);
            mem.WriteByte(movement_addr1 + offs_call2, BitConverter.GetBytes((((uint)tmp + movement_injection.Length) - (movement_addr1 + offs_call2 + 4))), 4);
            //
            mem.WriteByte(movement_addr2 + offs_call, BitConverter.GetBytes(((uint)tmp - (movement_addr2 + offs_call + 4))), 4);
            mem.WriteByte(movement_addr2 + offs_call2, BitConverter.GetBytes((((uint)tmp + movement_injection.Length) - (movement_addr2 + offs_call2 + 4))), 4);
            //Override rotation
            mem.WriteByte(rotation_addr, rotation_ovrd, rotation_ovrd.Length);
            //Modify call
            mem.WriteByte(rotation_addr + offs_call, BitConverter.GetBytes((((uint)tmp + movement_injection.Length + movement2_injection.Length) - (rotation_addr + offs_call + 4))), 4);
            //Code injection end
            */
            //Code injection
            IntPtr tmp = mem.Allocate((uint)respawn_injection.Length);
            mem.WriteByte((uint)tmp, respawn_injection, respawn_injection.Length);
            //Override respawn
            mem.WriteByte(respawn_addr, respawn_ovrd, respawn_ovrd.Length);
            //Modify jmp
            mem.WriteByte(respawn_addr + offs_jmp0, BitConverter.GetBytes((uint)tmp - (respawn_addr + offs_jmp0 + 4)), 4);
            mem.WriteByte((uint)tmp + offs_jmp1, BitConverter.GetBytes(respawn3_addr - ((uint)tmp + offs_jmp1 + 4)), 4);
            mem.WriteByte((uint)tmp + offs_jmp2, BitConverter.GetBytes(respawn3_addr - ((uint)tmp + offs_jmp2 + 4)), 4);
            mem.WriteByte((uint)tmp + offs_jmp3, BitConverter.GetBytes(respawn2_addr - ((uint)tmp + offs_jmp3 + 4)), 4);
            //Code injection end
            sock = client.Client;
            thd = new Thread(new ThreadStart(NetRec));
            thd.Start();
            thd2 = new Thread(new ThreadStart(NetSend));
            thd2.Start();
        }

        void NetRec()
        {
            try
            {
                byte[] buffer = new byte[BUFFER_SIZE];
                byte[] data = new byte[BUFFER_LEN];
                while (true)
                {
                    sock.Receive(buffer);
                    Array.Copy(buffer, 1, data, 0, BUFFER_LEN);
                    if (buffer[0] >= id)
                        buffer[0]--;
                    mem.WriteByte(NET_ADDR + CAR_OFFSET * buffer[0], data, BUFFER_LEN);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Source + " - " + e.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (game != null && !game.HasExited)
                    game.Kill();
                Environment.Exit(0);
            }
        }

        void NetSend()
        {
            try
            {
                byte[] buffer = new byte[BUFFER_LEN];
                while (true)
                {
                    Thread.Sleep(THD_SLEEP);
                    Array.Copy(mem.ReadBytes(PLAYER_ADDR, BUFFER_LEN), buffer, BUFFER_LEN);
                    sock.Send(buffer);
                }
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Source + " - " + e.Message, Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                if (game != null && !game.HasExited)
                    game.Kill();
                Environment.Exit(0);
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.SuspendLayout();
        }
    }

    class Progam
    {
        [STAThread]
        static void Main()
        {
            if (!File.Exists(Form1.game_exe))
            {
                MessageBox.Show("Game not found! (" + Form1.game_exe + ")",
                    "Cobra 11 Multiplayer Client 1.2 Alpha", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        }
    }
}