using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

/*
    수정전 읽어 볼 사항
    ParseTemplate  <- 문서 파싱
    ApplyPortRules <- 포트 자동 계산
    WriteConfigFile <- 출력

    [추가된 기능]
    1) servers.csv  : 서버 IP 목록을 CSV로 관리 (COMMON_HEADER SERVER 자동 삽입)
    2) $TEMPLATE:TitlePrefix#TYPE
         TYPE 에 속하는 모든 서버에 대해 설정을 자동 생성
         예) $TEMPLATE:Game#GAME  →  GAME01, GAME02 ... 각각 생성
    3) FOREACH:TYPE 라인
         해당 TYPE의 모든 서버에 대해 커넥션 라인을 반복 생성
         자기 자신(srcName == eachName)은 건너뜀
    4) 플레이스홀더
         {SELF}      현재 서버 전체 이름  (예: GAME01)
         {SELF_NUM}  현재 서버 숫자 접미사 (예: 01)
         {EACH}      FOREACH 반복 대상 서버 전체 이름
         {EACH_NUM}  FOREACH 반복 대상 서버 숫자 접미사

    포트 규칙 (GroupNumber 기반)
     ACCOUNT  <-> GAME      : 25000 + GroupNumber
     GAME     <-> GATE      : 10000 + gateNum*100 + GroupNumber
     GAME     <-> GAMEDB    : 30300 + GroupNumber
     GAME     <-> GAME      : 30000 + GroupNumber  (월드서버용)
     LOGIN    <-> GAMEDB    : 22000 + GroupNumber
     LOGIN    <-> GAME      : 21000 + GroupNumber
     TRADEDB  <-> GAME      : 25100 + GroupNumber
     UNION    <-> GAME      : 40100 + GroupNumber
 */

namespace ConnnectTool2
{
    class Program
    {
        // ─── 진입점 ───────────────────────────────────────────────────────────
        static void Main(string[] args)
        {
            const string templateFile = "config_template.txt";
            const string serversFile  = "servers.csv";

            if (!File.Exists(templateFile))
            {
                GenerateTemplate(templateFile);
                GenerateServersCSV(serversFile);
                Console.WriteLine($"'{templateFile}' 와 '{serversFile}' 가 생성되었습니다.");
                Console.WriteLine("IP 주소를 채운 뒤 다시 실행하세요.");
                return;
            }

            const string outputDir = "output";
            Directory.CreateDirectory(outputDir);

            // servers.csv 가 있으면 로드
            List<ServerEntry> csvServers = null;
            if (File.Exists(serversFile))
            {
                csvServers = LoadServersCSV(serversFile);
                Console.WriteLine($"[{serversFile}] 에서 서버 {csvServers.Count}개 로드");
            }

            List<string> commonHeader;
            var configs = ParseTemplate(templateFile, out commonHeader, csvServers);
            var servers = configs.ToDictionary(c => c.Name);

            ApplyPortRules(servers);
            AssignSoc(servers);

            foreach (var cfg in servers.Values)
            {
                var outputFile = $"output/{cfg.Name}.txt";
                WriteConfigFile(cfg, outputFile, commonHeader);
                Console.WriteLine($"Generated '{outputFile}'");
            }
        }

        // ─── servers.csv 관련 ─────────────────────────────────────────────────
        class ServerEntry
        {
            public string Name;
            public string PublicIP;
            public string LocalIP;
            public string Explain;
        }

        // CSV 로드 (헤더 행 1줄 건너뜀, 탭/쉼표 둘 다 허용)
        static List<ServerEntry> LoadServersCSV(string path)
        {
            var result = new List<ServerEntry>();
            bool firstLine = true;
            foreach (var raw in File.ReadAllLines(path))
            {
                var line = raw.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith(";") || line.StartsWith("#"))
                    continue;
                if (firstLine) { firstLine = false; continue; } // 헤더 행 스킵

                char sep = line.Contains('\t') ? '\t' : ',';
                var parts = line.Split(sep);
                if (parts.Length < 3) continue;

                result.Add(new ServerEntry
                {
                    Name     = parts[0].Trim(),
                    PublicIP = parts[1].Trim(),
                    LocalIP  = parts[2].Trim(),
                    Explain  = parts.Length > 3 ? parts[3].Trim() : ""
                });
            }
            return result;
        }

        // 예제 servers.csv 생성
        static void GenerateServersCSV(string path)
        {
            var lines = new List<string>
            {
                "Name,PublicIP,LocalIP,Explain",
                "LOGIN,1.2.3.4,192.168.0.1,Update Login",
                "UPDATE,1.2.3.4,192.168.0.2,Update",
                "ACCOUNT,1.2.3.4,192.168.0.3,AccDB",
                "GAME01,1.2.3.5,192.168.0.10,Gate Game",
                "GAME02,1.2.3.6,192.168.0.11,Gate Game",
                "GAMEDB01,1.2.3.7,192.168.0.20,GameDB",
                "GATE1,1.2.3.8,192.168.0.30,Gate",
            };
            File.WriteAllLines(path, lines);
        }

        // ─── 포트 규칙 ────────────────────────────────────────────────────────
        static void ApplyPortRules(Dictionary<string, ServerConfig> servers)
        {
            foreach (var cfg in servers.Values)
            {
                string srcName = cfg.SubName ?? cfg.Name;
                var srcType = Regex.Replace(srcName, @"\d+$", "");

                foreach (var conn in cfg.Connections)
                {
                    if (conn.Port != 0) continue; // 수동 지정 우선

                    var dstName = conn.ConnectIP.Split(':')[0];
                    var dstType = Regex.Replace(dstName, @"\d+$", "");

                    // ConnectIP 가 자기 자신이면 LocalIP 쪽이 상대방
                    if (srcName == dstName)
                    {
                        dstName = conn.LocalIP.Split(':')[0];
                        dstType = Regex.Replace(dstName, @"\d+$", "");
                    }

                    int port = 0;

                    if ((srcType == "ACCOUNT" && dstType == "GAME") ||
                        (srcType == "GAME"    && dstType == "ACCOUNT"))
                        port = 25000 + conn.GroupNumber;
                    else if ((srcType == "GAME" && dstType == "GATE") ||
                             (srcType == "GATE" && dstType == "GAME"))
                    {
                        int gateNum = srcType == "GATE"
                            ? int.Parse(Regex.Match(srcName, @"\d+").Value)
                            : int.Parse(Regex.Match(dstName, @"\d+").Value);
                        port = 10000 + gateNum * 100 + conn.GroupNumber;
                    }
                    else if ((srcType == "GAME"    && dstType == "GAMEDB") ||
                             (srcType == "GAMEDB"  && dstType == "GAME"))
                        port = 30300 + conn.GroupNumber;
                    else if ((srcType == "LOGIN"   && dstType == "GAMEDB") ||
                             (srcType == "GAMEDB"  && dstType == "LOGIN"))
                        port = 22000 + conn.GroupNumber;
                    else if ((srcType == "LOGIN"   && dstType == "GAME") ||
                             (srcType == "GAME"    && dstType == "LOGIN"))
                        port = 21000 + conn.GroupNumber;
                    // LOGIN ↔ GATE : 20100 + gateNum  (GATE1=20101, GATE2=20102 ...)
                    else if ((srcType == "LOGIN" && dstType == "GATE") ||
                             (srcType == "GATE"  && dstType == "LOGIN"))
                    {
                        int gateNum = srcType == "GATE"
                            ? int.Parse(Regex.Match(srcName, @"\d+").Value)
                            : int.Parse(Regex.Match(dstName, @"\d+").Value);
                        port = 20100 + gateNum;
                    }
                    // LOGIN ↔ ACCOUNT : 20000 (고정)
                    else if ((srcType == "LOGIN"   && dstType == "ACCOUNT") ||
                             (srcType == "ACCOUNT" && dstType == "LOGIN"))
                        port = 20000;
                    else if ((srcType == "TRADEDB" && dstType == "GAME") ||
                             (srcType == "GAME"    && dstType == "TRADEDB"))
                        port = 25100 + conn.GroupNumber;
                    else if (srcType == "GAME" && dstType == "GAME")
                        port = 30000 + conn.GroupNumber;
                    else if ((srcType == "UNION"   && dstType == "GAME") ||
                             (srcType == "GAME"    && dstType == "UNION"))
                        port = 40100 + conn.GroupNumber;

                    if (port > 0) conn.Port = port;
                }
            }
        }

        // ─── Soc 순차 배정 ────────────────────────────────────────────────────
        static void AssignSoc(Dictionary<string, ServerConfig> servers)
        {
            foreach (var cfg in servers.Values.OrderBy(c => c.Name))
            {
                int i = 0;
                foreach (var conn in cfg.Connections)
                    conn.Soc = i++;
            }
        }

        // ─── 템플릿 자동 생성 ─────────────────────────────────────────────────
        static void GenerateTemplate(string path)
        {
            var lines = new List<string>
            {
                "; ════════════════════════════════════════════════════════",
                "; 서버 설정 템플릿  (config_template.txt)",
                "; ════════════════════════════════════════════════════════",
                ";",
                "; [COMMON_HEADER]",
                ";   모든 출력 파일 상단에 붙을 내용.",
                ";   servers.csv 가 있으면 SERVER 줄을 자동으로 교체합니다.",
                ";   없으면 아래에 직접 SERVER 줄을 작성하세요.",
                ";",
                "COMMON_HEADER",
                "@\teyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.TOKEN_HERE",
                ";\tNAME\tPUBLIC\t\tLOCAL\t\tEXPLAIN",
                "; (servers.csv 사용 시 아래 SERVER 줄은 자동 삽입됩니다)",
                "SERVER\tLOGIN\t1.2.3.4\t192.168.0.1\tUpdate, Login",
                "SERVER\tGATE1\t1.2.3.8\t192.168.0.30\tGate",
                "",
                "; ────────────────────────────────────────────────────────",
                "; [SERVER_LIST]",
                ";   생성할 서버 목록. Type:번호리스트 (콤마/범위 허용)",
                ";   ACCOUNT 는 번호 없이 생성됨.",
                "; ────────────────────────────────────────────────────────",
                "SERVER_LIST",
                "ACCOUNT:1",
                "GAME:01,02",
                "GAMEDB:01",
                "GATE:1",
                "",
                "; ────────────────────────────────────────────────────────",
                "; [$TEMPLATE:표시접두사#TYPE]",
                ";   SERVER_LIST 의 TYPE 서버 각각에 대해 자동 생성.",
                ";   표시접두사 + 숫자접미사 = 출력 파일명 / $ 타이틀.",
                ";   예) $TEMPLATE:Game#GAME  →  Game01.txt, Game02.txt",
                ";",
                "; [FOREACH:TYPE  LisOrConn - Comment LocalIP ConnectIP GroupNum RecvBuf SendBuf ReadQ SendQ]",
                ";   TYPE 의 모든 서버에 대해 커넥션 라인을 반복 생성.",
                ";   자기 자신(서버 이름 동일)은 건너뜁니다.",
                ";",
                "; 플레이스홀더",
                ";   {SELF}      현재 서버 전체 이름  (예: GAME01)",
                ";   {SELF_NUM}  현재 서버 숫자 부분  (예: 01)",
                ";   {EACH}      FOREACH 대상 서버 전체 이름",
                ";   {EACH_NUM}  FOREACH 대상 서버 숫자 부분",
                ";",
                "; GroupNumber 규칙 (포트 자동 계산)",
                ";   ACCOUNT <-> GAME   : 25000 + GroupNumber",
                ";   GAME    <-> GATE   : 10000 + gateNum*100 + GroupNumber",
                ";   GAME    <-> GAMEDB : 30300 + GroupNumber",
                ";   LOGIN   <-> GAME   : 21000 + GroupNumber",
                ";   LOGIN   <-> GAMEDB : 22000 + GroupNumber",
                "; ────────────────────────────────────────────────────────",
                "",
                "; === ACCOUNT 서버 (1개) ===",
                "$TEMPLATE:AccDB#ACCOUNT",
                "; LisOrConn - Comment  LocalIP  ConnectIP  [GroupNumber]  RecvBuf  SendBuf  ReadQ  SendQ",
                "0 - User ACCOUNT:LOCAL ACCOUNT:LOCAL X 0 4096 4096 4096 4096",
                "FOREACH:GAME 0 - Game{EACH_NUM} {EACH}:LOCAL ACCOUNT:LOCAL {EACH_NUM} 4096 4096 10240000 10240000",
                "1 - Login ACCOUNT:LOCAL LOGIN:LOCAL X 20000 4096 4096 1024000 1024000",
                "",
                "; === GAME 서버 (FOREACH 로 GATE, GAMEDB 자동 연결) ===",
                "$TEMPLATE:Game#GAME",
                "0 - User 127.0.0.1 127.0.0.1 X 0 160000 160000 50000 50000",
                "FOREACH:GATE 0 - Gate{EACH_NUM} {EACH}:LOCAL {SELF}:LOCAL {SELF_NUM} 160000 160000 10000000 30000000",
                "1 - AccDB {SELF}:LOCAL ACCOUNT:LOCAL {SELF_NUM} 160000 160000 10240000 10240000",
                "1 - Login {SELF}:LOCAL LOGIN:LOCAL {SELF_NUM} 160000 160000 1000000 1000000",
                "FOREACH:GAMEDB 1 - GameDB{EACH_NUM} {SELF}:LOCAL {EACH}:LOCAL {SELF_NUM} 160000 160000 60000000 60000000",
                "",
                "; === GAMEDB 서버 ===",
                "$TEMPLATE:GameDB#GAMEDB",
                "0 - User 127.0.0.1 127.0.0.1 X 0 4096 4096 4096 4096",
                "FOREACH:GAME 0 - Game{EACH_NUM} {EACH}:LOCAL {SELF}:LOCAL {EACH_NUM} 160000 160000 10240000 10240000",
                "1 - Login {SELF}:LOCAL LOGIN:LOCAL X 22001 4096 4096 5120000 5120000",
                "",
                "; === GATE 서버 (FOREACH 로 GAME 자동 연결) ===",
                "; 주의: 유저 포트(14001 등)는 서버마다 다를 수 있으므로 $SERVER 로 개별 지정 권장",
                "$SERVER:GATE1",
                "0 - User GATE1:LOCAL GATE1:LOCAL X 14001 8192 8192 512000 512000",
                "1 - Update GATE1:LOCAL UPDATE:LOCAL X 15500 4096 4096 1000000 1000000",
                "1 - Game01 GATE1:LOCAL GAME01:LOCAL 1 160000 160000 30000000 30000000",
                "1 - Game02 GATE1:LOCAL GAME02:LOCAL 2 160000 160000 30000000 30000000",
                "1 - Login GATE1:LOCAL LOGIN:LOCAL X 20101 160000 160000 5120000 5120000",
                "",
                "; GATE 가 여러 개라면 $SERVER:GATE2, $SERVER:GATE3 블록을 복사해서 추가하거나",
                "; $TEMPLATE:GATE 로 변경하고 유저 포트를 X 고정값으로 통일하세요.",
            };
            File.WriteAllLines(path, lines);
        }

        // ─── 템플릿 파싱 ─────────────────────────────────────────────────────
        static List<ServerConfig> ParseTemplate(
            string path,
            out List<string> commonHeader,
            List<ServerEntry> csvServers)
        {
            var lines = File.ReadAllLines(path);
            var configs = new List<ServerConfig>();
            commonHeader = new List<string>();

            // ── COMMON_HEADER 읽기 ──
            int chIdx = Array.IndexOf(lines, "COMMON_HEADER");
            if (chIdx >= 0)
            {
                int idx = chIdx;
                while (idx < lines.Length && !string.IsNullOrWhiteSpace(lines[idx]))
                {
                    idx++;
                    if (idx < lines.Length && !string.IsNullOrWhiteSpace(lines[idx]))
                        commonHeader.Add(lines[idx]);
                }
            }

            // servers.csv 가 있으면 commonHeader 의 SERVER 줄을 교체
            if (csvServers != null && csvServers.Count > 0)
            {
                commonHeader.RemoveAll(l => Regex.IsMatch(l.TrimStart(), @"^SERVER\s"));
                foreach (var s in csvServers)
                    commonHeader.Add($"SERVER\t{s.Name}\t{s.PublicIP}\t{s.LocalIP}\t{s.Explain}");
            }

            // ── SERVER_LIST 파싱 → serversByType ──
            var serversByType = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            int slIdx = Array.IndexOf(lines, "SERVER_LIST");
            if (slIdx >= 0)
            {
                for (int i = slIdx + 1; i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]); i++)
                {
                    var raw = lines[i].Trim();
                    if (raw.StartsWith(";")) continue;
                    var colonPos = raw.IndexOf(':');
                    if (colonPos < 0) continue;
                    var type = raw.Substring(0, colonPos).Trim();
                    var rest = raw.Substring(colonPos + 1).Trim();
                    var names = new List<string>();

                    foreach (var seg in rest.Split(','))
                    {
                        var s = seg.Trim();
                        if (s.Contains("-"))
                        {
                            // 범위: "1-3" → 1,2,3  /  "01-03" → 01,02,03 (첫 번째 값의 자릿수 기준)
                            var rangeParts = s.Split('-');
                            int from = int.Parse(rangeParts[0].Trim());
                            int to   = int.Parse(rangeParts[1].Trim());
                            int digits = rangeParts[0].Trim().Length; // 원본 자릿수 유지
                            string fmt = digits > 1 ? "D" + digits : null;
                            for (int v = from; v <= to; v++)
                                names.Add(BuildServerName(type, fmt != null ? v.ToString(fmt) : v.ToString()));
                        }
                        else if (int.TryParse(s, out _))
                        {
                            // 명시 값: 원본 문자열 그대로 ("01" → "01", "1" → "1")
                            names.Add(BuildServerName(type, s));
                        }
                    }
                    serversByType[type] = names;
                }
            }

            // ── $TEMPLATE / $SERVER 블록 파싱 ──
            for (int i = 0; i < lines.Length; i++)
            {
                var raw = lines[i].Trim();

                if (raw.StartsWith("$TEMPLATE:"))
                {
                    // $TEMPLATE:TitlePrefix#TYPE  또는  $TEMPLATE:TYPE
                    var load = raw.Substring("$TEMPLATE:".Length).Trim();
                    ParseBlockHeader(load, out string titlePrefix, out string type);

                    // 블록 본문 수집 (빈 줄 또는 다음 $ 키워드까지)
                    var templateLines = CollectBlockLines(lines, i + 1);

                    // TYPE 에 속하는 각 서버에 대해 ServerConfig 생성
                    if (!serversByType.TryGetValue(type, out var typeServers)) continue;

                    foreach (var serverName in typeServers)
                    {
                        var numSuffix = Regex.Match(serverName, @"\d+$").Value;
                        var displayName = titlePrefix + numSuffix;

                        var cfg = new ServerConfig { Name = displayName, SubName = serverName };
                        ExpandTemplateLines(templateLines, cfg, serverName, numSuffix, serversByType);
                        configs.Add(cfg);
                    }
                }
                else if (raw.StartsWith("$SERVER:"))
                {
                    // 기존 $SERVER 블록 (이전 버전 호환 + GATE 같이 개별 지정이 필요한 서버용)
                    var load = raw.Substring("$SERVER:".Length).Trim();
                    ParseBlockHeader(load, out string displayName, out string subName);

                    var cfg = new ServerConfig { Name = displayName, SubName = string.IsNullOrEmpty(subName) ? null : subName };

                    var blockLines = CollectBlockLines(lines, i + 1);
                    foreach (var bline in blockLines)
                    {
                        if (bline.StartsWith(";") || bline.StartsWith("$")) continue;
                        TryAddConnection(cfg, bline);
                    }
                    configs.Add(cfg);
                }
            }

            return configs;
        }

        // ─── 헬퍼: 서버 이름 생성 ────────────────────────────────────────────
        // ACCOUNT 는 숫자 없이, 나머지는 numStr 그대로 붙임 (D2 강제 없음)
        static string BuildServerName(string type, string numStr)
        {
            // 번호 접미사 없이 단독으로 존재하는 서버 타입
            if (type.Equals("ACCOUNT", StringComparison.OrdinalIgnoreCase) ||
                type.Equals("LOGIN",   StringComparison.OrdinalIgnoreCase) ||
                type.Equals("UPDATE",  StringComparison.OrdinalIgnoreCase))
                return type.ToUpper();
            return type.ToUpper() + numStr;
        }

        // ─── 헬퍼: "TitlePrefix#TYPE" 또는 "TYPE" 파싱 ──────────────────────
        static void ParseBlockHeader(string load, out string title, out string type)
        {
            var h = load.IndexOf('#');
            if (h >= 0)
            {
                title = load.Substring(0, h).Trim();
                type  = load.Substring(h + 1).Trim();
            }
            else
            {
                title = load.Trim();
                type  = load.Trim();
            }
        }

        // ─── 헬퍼: 블록 본문 줄 수집 (빈 줄 또는 $ 키워드까지) ──────────────
        static List<string> CollectBlockLines(string[] lines, int startIdx)
        {
            var result = new List<string>();
            for (int j = startIdx; j < lines.Length; j++)
            {
                var l = lines[j].Trim();
                if (string.IsNullOrEmpty(l)) break;
                if (l.StartsWith("$TEMPLATE:") || l.StartsWith("$SERVER:")) break;
                result.Add(l);
            }
            return result;
        }

        // ─── 헬퍼: $TEMPLATE 블록 확장 ───────────────────────────────────────
        static void ExpandTemplateLines(
            List<string> templateLines,
            ServerConfig cfg,
            string serverName,
            string numSuffix,
            Dictionary<string, List<string>> serversByType)
        {
            foreach (var tline in templateLines)
            {
                if (tline.StartsWith(";")) continue;

                if (tline.StartsWith("FOREACH:"))
                {
                    int spaceIdx = tline.IndexOf(' ');
                    if (spaceIdx < 0) continue;
                    var directive   = tline.Substring(0, spaceIdx);         // "FOREACH:GATE"
                    var connTmpl    = tline.Substring(spaceIdx + 1).Trim(); // 커넥션 템플릿

                    // FOREACH:TYPE[:FILTER] - 현재는 TYPE 만 지원
                    var foreachType = directive.Substring("FOREACH:".Length).Split(':')[0];
                    if (!serversByType.TryGetValue(foreachType, out var eachList)) continue;

                    foreach (var eachName in eachList)
                    {
                        // 자기 자신(SubName 또는 Name)은 건너뜀
                        if (eachName.Equals(serverName, StringComparison.OrdinalIgnoreCase)) continue;

                        var eachNum = Regex.Match(eachName, @"\d+$").Value;
                        var expanded = connTmpl
                            .Replace("{SELF}",     serverName)
                            .Replace("{SELF_NUM}", numSuffix)
                            .Replace("{EACH}",     eachName)
                            .Replace("{EACH_NUM}", eachNum);

                        TryAddConnection(cfg, expanded);
                    }
                }
                else
                {
                    var expanded = tline
                        .Replace("{SELF}",     serverName)
                        .Replace("{SELF_NUM}", numSuffix);
                    TryAddConnection(cfg, expanded);
                }
            }
        }

        // ─── 헬퍼: 커넥션 라인 파싱 후 추가 ─────────────────────────────────
        static void TryAddConnection(ServerConfig cfg, string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";")) return;

            var parts = Regex.Split(line.Trim(), @"\s+");
            if (parts.Length < 10) return;

            bool custom = parts[5] == "X";
            int group = 0;
            if (!custom)
            {
                if (!int.TryParse(parts[5], out group) || group < 0 || group > 100)
                    throw new ArgumentOutOfRangeException($"GroupNumber 는 0~100 사이여야 합니다: '{parts[5]}' (라인: {line})");
            }

            int port  = custom ? int.Parse(parts[6]) : 0;
            int recv  = custom ? int.Parse(parts[7]) : int.Parse(parts[6]);
            int send  = custom ? int.Parse(parts[8]) : int.Parse(parts[7]);
            int rq    = custom ? int.Parse(parts[9]) : int.Parse(parts[8]);
            int sq    = custom ? int.Parse(parts[10]) : int.Parse(parts[9]);

            // Soc 는 '-' 또는 숫자. AssignSoc 가 덮어쓰므로 0 으로 초기화.
            cfg.Connections.Add(new Connection
            {
                LisOrConn      = int.Parse(parts[0]),
                Soc            = 0,
                Comment        = parts[2],
                LocalIP        = parts[3],
                ConnectIP      = parts[4],
                GroupNumber    = group,
                Port           = port,
                RecvBuffer     = recv,
                SendBuffer     = send,
                ReadQueueBuffer  = rq,
                SendQueueBuffer  = sq,
            });
        }

        // ─── 결과 파일 작성 ───────────────────────────────────────────────────
        static void WriteConfigFile(ServerConfig cfg, string path, List<string> commonHeader)
        {
            var lines = new List<string> { $"$\t{cfg.Name}" };

            lines.AddRange(commonHeader);

            lines.Add(";---------------------------------------------------------------------------------------------------------------------------------------------------------");
            lines.Add(";Lis or conn\tSoc#\t\tComments\tLocalIP\t\tConnectIP\tPort\tRecsocBuffer\tSedoscBuffer\tReadQueueBuffer\tSendQueueBuffer");
            lines.Add(";---------------------------------------------------------------------------------------------------------------------------------------------------------");

            lines.AddRange(cfg.Connections.Select(c =>
                $"{c.LisOrConn}\t\t{c.Soc}\t\t{c.Comment}\t\t{c.LocalIP}\t{c.ConnectIP}\t{c.Port}\t{c.RecvBuffer}\t{c.SendBuffer}\t{c.ReadQueueBuffer}\t{c.SendQueueBuffer}"));

            File.WriteAllLines(path, lines);
        }
    }
}
