using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class SecureChatServer
{
    private static List<TcpClient> clientList = new List<TcpClient>();
    private static string serverPassword = "default_password"; // 파일 읽기 실패 시 기본값

    static void Main()
    {
        LoadServerConfig();
        TcpListener server = new TcpListener(IPAddress.Any, 5000);
        server.Start();
        Console.WriteLine($">>> 서버 시작 (Port: 5000, PW: {serverPassword})");

        while (true)
        {
            TcpClient client = server.AcceptTcpClient();

            // [추가] 접속 시도 단계에서 바로 IP 출력
            string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            Console.WriteLine($"\n[접속 시도] IP: {clientIP} 에서 연결을 시도했습니다.");

            Thread clientThread = new Thread(HandleClient);
            clientThread.Start(client);
        }
    }

    private static void LoadServerConfig()
    {
        string filePath = "server_config.txt";
        try
        {
            if (File.Exists(filePath))
            {
                string[] lines = File.ReadAllLines(filePath);
                foreach (string line in lines)
                {
                    // 1. 주석(//) 제거 로직
                    string processedLine = line;
                    int commentIndex = line.IndexOf("//");
                    if (commentIndex >= 0)
                    {
                        processedLine = line.Substring(0, commentIndex);
                    }

                    processedLine = processedLine.Trim(); // 공백 제거
                    if (string.IsNullOrEmpty(processedLine)) continue; // 빈 줄 스킵

                    // 2. 설정 파싱
                    if (processedLine.StartsWith("password:"))
                    {
                        serverPassword = processedLine.Replace("password:", "").Trim();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("설정 파일 로드 중 오류: " + ex.Message);
        }
    }

    private static void HandleClient(object obj)
    {
        TcpClient client = (TcpClient)obj;
        NetworkStream stream = client.GetStream();

        // 접속한 클라이언트의 IP 주소 추출
        string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
        string nickname = "";

        try
        {
            byte[] buffer = new byte[1024];

            // 1. 비밀번호 확인 로직 (기존과 동일)
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string receivedPassword = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            if (receivedPassword != serverPassword)
            {
                Console.WriteLine($"[차단] {clientIP}: 비밀번호 불일치");
                byte[] failMsg = Encoding.UTF8.GetBytes("FAIL: 비밀번호가 틀렸습니다.");
                stream.Write(failMsg, 0, failMsg.Length);
                client.Close();
                return;
            }

            // 인증 성공 알림
            byte[] successMsg = Encoding.UTF8.GetBytes("SUCCESS");
            stream.Write(successMsg, 0, successMsg.Length);

            // 2. 닉네임 수신
            bytesRead = stream.Read(buffer, 0, buffer.Length);
            nickname = Encoding.UTF8.GetString(buffer, 0, bytesRead).Trim();

            lock (clientList) { clientList.Add(client); }

            // [핵심 수정] 입장 시 닉네임과 IP를 함께 브로드캐스트
            string welcomeMsg = $"[알림] {nickname}님({clientIP})이 입장하셨습니다.";
            Console.WriteLine(welcomeMsg); // 서버 콘솔 기록
                                           // 입장 알림 (이건 모두가 봐야 하므로 null을 넘기거나 별도 처리)
            Broadcast($"[알림] {nickname}님({clientIP})이 입장하셨습니다.", null);

            // 일반 메시지 루프
            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                // 본인을 제외하고 나머지 사람들에게만 전달
                Broadcast($"[{nickname}]: {message}", client);
            }
        }
        catch { /* 연결 종료 처리 */ }
        finally
        {
            lock (clientList) { clientList.Remove(client); }
            if (!string.IsNullOrEmpty(nickname))
            {
                // 퇴장 시에도 정보를 같이 뿌려주면 관리가 편합니다.
                string exitMsg = $"[알림] {nickname}님({clientIP})이 퇴장하셨습니다.";
                Console.WriteLine(exitMsg);
                Broadcast(exitMsg,null);
            }
            client.Close();
        }
    }

    private static void Broadcast(string message, TcpClient sender)
    {
        Console.WriteLine(message);
        byte[] buffer = Encoding.UTF8.GetBytes(message);

        lock (clientList)
        {
            foreach (var client in clientList)
            {
                // [핵심] 메시지를 보낸 사람(sender)과 리스트의 client가 같으면 건너뜀
                if (client == sender) continue;

                try
                {
                    client.GetStream().Write(buffer, 0, buffer.Length);
                }
                catch { /* 연결 끊긴 클라이언트는 무시 */ }
            }
        }
    }
}