using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class MultiChatClient
{
    static void Main()
    {
        // 1. 설정 파일에서 서버 정보 읽기
        string configPath = "client_config.txt";
        string ip = "127.0.0.1";
        int port = 5000;

        if (File.Exists(configPath))
        {
            string rawContent = File.ReadAllText(configPath);

            // 주석(//) 제거
            int commentIndex = rawContent.IndexOf("//");
            string cleanContent = commentIndex >= 0
                ? rawContent.Substring(0, commentIndex).Trim()
                : rawContent.Trim();

            if (!string.IsNullOrEmpty(cleanContent))
            {
                string[] parts = cleanContent.Split(':');
                if (parts.Length == 2)
                {
                    ip = parts[0];
                    port = int.Parse(parts[1]);
                }
            }
        }

        try
        {
            TcpClient client = new TcpClient(ip, port);
            NetworkStream stream = client.GetStream();
            Console.WriteLine($">>> {ip}:{port} 서버에 연결되었습니다.");

            // 2. 서버 비밀번호 입력 및 전송
            Console.Write("서버 비밀번호를 입력하세요: ");
            string password = Console.ReadLine();
            byte[] passData = Encoding.UTF8.GetBytes(password);
            stream.Write(passData, 0, passData.Length);

            // 서버로부터 인증 결과 수신 ("SUCCESS" 또는 "FAIL...")
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string authResult = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (authResult != "SUCCESS")
            {
                Console.WriteLine($"[인증 실패]: {authResult}");
                return; // 프로그램 종료
            }
            Console.WriteLine(">>> 인증 성공!");

            // 3. 닉네임 설정 및 전송
            Console.Write("사용할 닉네임을 입력하세요: ");
            string nickname = Console.ReadLine();
            byte[] nickData = Encoding.UTF8.GetBytes(nickname);
            stream.Write(nickData, 0, nickData.Length);

            // 4. 수신 전용 쓰레드 시작 (상대방 메시지 출력용)
            Thread receiveThread = new Thread(() => ReceiveMessages(stream));
            receiveThread.IsBackground = true; // 메인 종료 시 함께 종료
            receiveThread.Start();

            // 5. 송신 루프 (내 메시지 전송용)
            Console.WriteLine(">>> 채팅을 시작합니다! (종료하려면 Ctrl+C)");
            while (true)
            {
                string msg = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(msg)) continue;

                byte[] msgData = Encoding.UTF8.GetBytes(msg);
                stream.Write(msgData, 0, msgData.Length);

                Console.SetCursorPosition(0, Console.CursorTop - 1);
                Console.ForegroundColor = ConsoleColor.DarkGray; // 내 메시지 색상 변경
                Console.WriteLine($"[{nickname}] >> {msg}");
                Console.ResetColor(); // 색상 원래대로 복구
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("오류 발생: " + e.Message);
        }
    }

    private static void ReceiveMessages(NetworkStream stream)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (true)
            {
                int bytes = stream.Read(buffer, 0, buffer.Length);
                if (bytes <= 0) break;
                Console.WriteLine(Encoding.UTF8.GetString(buffer, 0, bytes));
            }
        }
        catch { Console.WriteLine("\n[서버와의 연결이 끊어졌습니다]"); }
    }
}