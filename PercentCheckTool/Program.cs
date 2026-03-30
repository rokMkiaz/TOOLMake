using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace CsvGroupProbabilityChecker
{
    class Program
    {
        static void Main(string[] args)
        {
            // 실행파일(.exe)과 같은 디렉터리에서 InputFile.csv 로드
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var templatePath = Path.Combine(exeDir, "InputFile.csv");

            if (!File.Exists(templatePath))
            {
                Console.WriteLine($"템플릿이 없습니다: {templatePath}");
                return;
            }

            string[] tplLines;
            try
            {
                using (var fs = new FileStream(templatePath,
                                               FileMode.Open,
                                               FileAccess.Read,
                                               FileShare.ReadWrite))
                using (var sr = new StreamReader(fs))
                {
                    var temp = new List<string>();
                    string row;
                    while ((row = sr.ReadLine()) != null)
                        temp.Add(row);
                    tplLines = temp.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"템플릿 읽기 오류: {ex.Message}");
                return;
            }

            foreach (var raw in tplLines)
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") )
                    continue;

                // 탭으로 분리하여 7개 항목 읽기: 파일, 그룹컬럼, 서브그룹컬럼, 확률컬럼, 최대합계, 그룹 인덱스컬럼, 전체 인덱스컬럼
                var parts = line.Split('\t')
                                .Select(p => p.Trim())
                                .ToArray();

                if (parts.Length != 7)
                {
                    Console.WriteLine($"[파싱 오류] 6개 항목 필요(CSV 구분): \"{line}\"");
                    continue;
                }

                var csvRel = "_Data/"+parts[0];
                var groupCol = parts[1];
                var subCol = parts[2];
                var isSubCol = subCol == "0" ? string.Empty : subCol;

                var probCol = parts[3];
                if (!double.TryParse(parts[4], out var targetSum))
                {
                    Console.WriteLine($"[파싱 오류] 최대확률 숫자 변환 실패: \"{parts[4]}\"");
                    continue;
                }
                var rawGroupIndex = parts[5];  // 중복 인덱스 검사 컬럼명 ("{없음}"일 경우 스킵)
                var rawGlobalIndex = parts[6];
                var groupIndexCol = rawGroupIndex == "0" ? string.Empty : rawGroupIndex;
                var globalIndexCol = rawGlobalIndex == "0" ? string.Empty : rawGlobalIndex;


                var csvPath = Path.Combine(exeDir, csvRel);
                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"[파일 없음] {csvPath}");
                    continue;
                }
                if (targetSum == 0)
                    ProcessFile_NotPercent(exeDir, csvPath, groupCol, groupIndexCol, globalIndexCol);
                else if (string.IsNullOrEmpty(isSubCol))
                    ProcessFile_GroupOnly(exeDir, csvPath, groupCol, probCol, targetSum, groupIndexCol, globalIndexCol);
                else
                    ProcessFile_GroupAndSub(exeDir, csvPath, groupCol, subCol, probCol, targetSum, groupIndexCol, globalIndexCol);
            }

            Console.WriteLine("모든 파일 처리 완료.");
        }
        static void ProcessFile_NotPercent(string exeDir, string csvPath,string groupCol,string groupIndexCol,string globalIndexCol)
        {
            // 파일 공유 모드로 읽기
            string[] lines;
            using (var fs = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                var temp = new List<string>();
                string row;
                while ((row = sr.ReadLine()) != null)
                    temp.Add(row);
                lines = temp.ToArray();
            }

            // 헤더 찾기: 구분자별로 컬럼을 분리한 뒤 정확히 매칭
            int hdr = Array.FindIndex(lines, l =>
            {
                var cols = l.Split(new[] { ',', ';', '	' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(c => c.Trim());
                return cols.Contains(groupCol);
            });
            if (hdr < 0)
            {
                Console.WriteLine($"[{Path.GetFileName(csvPath)}] 헤더({groupCol}) 못 찾음");
                return;
            }

            // 헤더에서 컬럼 인덱스 구하기
            var headers = lines[hdr].Split(new[] { ',', ';', '	' }, StringSplitOptions.RemoveEmptyEntries)
                                     .Select(h => h.Trim()).ToArray();
            int gi = Array.IndexOf(headers, groupCol);
            int gii = string.IsNullOrEmpty(groupIndexCol) ? -1 : Array.IndexOf(headers, groupIndexCol);
            int globii = string.IsNullOrEmpty(globalIndexCol) ? -1 : Array.IndexOf(headers, globalIndexCol);

            var groupIndices = new Dictionary<string, HashSet<string>>();
            var firstGroupDup = new Dictionary<string, string>();
            var globalSeen = new HashSet<string>();
            string firstGlobalDup = null;

            for (int i = hdr + 1; i < lines.Length; i++)
            {
                var cols = lines[i].Split(new[] { ',', ';', '	' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(c => c.Trim()).ToArray();
                if (cols.Length <= gi) continue;

                var g = cols[gi];
                if (string.IsNullOrEmpty(g)) continue;

                // 그룹별 중복 인덱스 체크
                if (gii >= 0 && cols.Length > gii)
                {
                    var idx = cols[gii];
                    if (!groupIndices.ContainsKey(g))
                        groupIndices[g] = new HashSet<string>();
                    if (groupIndices[g].Contains(idx))
                    {
                        if (!firstGroupDup.ContainsKey(g))
                            firstGroupDup[g] = idx;
                    }
                    else
                        groupIndices[g].Add(idx);
                }

                // 전체 중복 인덱스 체크
                if (globii >= 0 && cols.Length > globii)
                {
                    var gidx = cols[globii];
                    if (globalSeen.Contains(gidx))
                    {
                        if (firstGlobalDup == null)
                            firstGlobalDup = gidx;
                    }
                    else
                        globalSeen.Add(gidx);
                }
            }

            var outputDir = Path.Combine(exeDir, "output");
            Directory.CreateDirectory(outputDir);
            var outPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(csvPath) + "_no_percnt.txt");

            using (var sw = new StreamWriter(outPath))
            {
                // 전체 인덱스 첫 중복을 상단에
                if (!string.IsNullOrEmpty(firstGlobalDup))
                    sw.WriteLine($"{globalIndexCol} 중복 INDEX: {firstGlobalDup}");
                // 그룹별 첫 중복만 출력
                foreach (var kv in firstGroupDup)
                    sw.WriteLine($"{groupCol} '{kv.Key}' 중복 INDEX: {kv.Value}");
                if (firstGroupDup.Count == 0 && string.IsNullOrEmpty(firstGlobalDup))
                    sw.WriteLine("No duplicates found.");
            }

            // 콘솔 출력
            Console.WriteLine($"[output/{Path.GetFileName(outPath)}] 생성 (중복만):");
            if (!string.IsNullOrEmpty(firstGlobalDup))
                Console.WriteLine($"{globalIndexCol} DUP INDEX: {firstGlobalDup}");
            foreach (var kv in firstGroupDup)
                Console.WriteLine($"'{groupCol}' '{kv.Key}' DUP INDEX: {kv.Value}");
            if (firstGroupDup.Count == 0 && string.IsNullOrEmpty(firstGlobalDup))
                Console.WriteLine("No duplicates found.");
            Console.WriteLine();
        }
        static void ProcessFile_GroupOnly(string exeDir, string csvPath, string groupCol, string probCol, double targetSum, string indexCol, string globalIndexCol)
        {
            string[] lines;
            using (var fs = new FileStream(csvPath,
                                           FileMode.Open,
                                           FileAccess.Read,
                                           FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                var temp = new List<string>();
                string row;
                while ((row = sr.ReadLine()) != null)
                    temp.Add(row);
                lines = temp.ToArray();
            }

            int hdr = Array.FindIndex(lines, l => l.Contains(groupCol) && l.Contains(probCol));
            if (hdr < 0)
            {
                Console.WriteLine($"[{Path.GetFileName(csvPath)}] 헤더({groupCol},{probCol}) 못 찾음");
                return;
            }

            var headers = lines[hdr]
                .Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim())
                .ToArray();

            int gi = Array.IndexOf(headers, groupCol);
            int pi = Array.IndexOf(headers, probCol);
            int gii = -1, globii = -1;

            bool doGroupIndex = !string.IsNullOrEmpty(indexCol);
            bool doGlobalIndex = !string.IsNullOrEmpty(globalIndexCol);
            if (doGroupIndex)
            {
                gii = Array.IndexOf(headers, indexCol);
                if (gii < 0)
                {
                    Console.WriteLine($"[{Path.GetFileName(csvPath)}] 인덱스 컬럼 못 찾음: {indexCol}");
                    return;
                }
            }
            if (doGlobalIndex)
            {
                globii = Array.IndexOf(headers, globalIndexCol);
                if (globii < 0)
                {
                    Console.WriteLine($"[{Path.GetFileName(csvPath)}] 전체 인덱스 컬럼 못 찾음: {globalIndexCol}");
                    return;
                }
            }

            var sums = new Dictionary<string, double>();
            var groupIndices = new Dictionary<string, HashSet<string>>();
            var firstGroupDup = new Dictionary<string, string>();
            var globalSeen = new HashSet<string>();
            string firstGlobalDup = null;

            for (int i = hdr + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";")) continue;

                var cols = lines[i]
                    .Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();
                if (cols.Length <= Math.Max(gi, pi)) continue;

                var g = cols[gi];
                if (string.IsNullOrEmpty(g)) continue;
                if (!double.TryParse(cols[pi], out var p)) continue;

                if (sums.ContainsKey(g)) sums[g] += p;
                else sums[g] = p;

                if (doGroupIndex && cols.Length > gii)
                {
                    var idx = cols[gii];
                    if (!groupIndices.ContainsKey(g))
                        groupIndices[g] = new HashSet<string>();
                    if (groupIndices[g].Contains(idx))
                    {
                        if (!firstGroupDup.ContainsKey(g))
                            firstGroupDup[g] = idx;
                    }
                    else groupIndices[g].Add(idx);
                }
                if (doGlobalIndex && cols.Length > globii)
                {
                    var gidx = cols[globii];
                    if (globalSeen.Contains(gidx))
                    {
                        if (firstGlobalDup == null) firstGlobalDup = gidx;
                    }
                    else globalSeen.Add(gidx);
                }
            }

            var outputDir = Path.Combine(exeDir, "output");
            Directory.CreateDirectory(outputDir);
            var outPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(csvPath) + ".txt");
            using (var sw = new StreamWriter(outPath))
            {

                if (!string.IsNullOrEmpty(firstGlobalDup))
                    sw.WriteLine($"'{globalIndexCol}' 중복 INDEX: {firstGlobalDup}");
                foreach (var kv in sums)
                {
                    var err = new List<string>();
                    if (kv.Value != targetSum) err.Add("!!!!ERROR!!!!");
                    if (firstGroupDup.TryGetValue(kv.Key, out var dupVal))
                        err.Add($"!!!!{indexCol} 중복 INDEX : {dupVal}!!!!");
                    sw.WriteLine($"{groupCol} {kv.Key} -> {kv.Value} / {targetSum}"
                        + (err.Any() ? "\t" + string.Join(" ", err) : string.Empty));
                }
            }

            Console.WriteLine($"[output/{Path.GetFileName(outPath)}] 생성 (그룹만):");
            foreach (var kv in sums)
            {
                var err = new List<string>();
                if (kv.Value != targetSum) err.Add("!!!!ERROR!!!!");
                if (firstGroupDup.TryGetValue(kv.Key, out var dupVal))
                    err.Add($"!!!!DUP INDEX {dupVal}!!!!");
                Console.WriteLine($"{kv.Key} 그룹 -> {kv.Value} / {targetSum}"
                    + (err.Any() ? "\t" + string.Join(" ", err) : string.Empty));
            }
            Console.WriteLine();
        }
        static void ProcessFile_GroupAndSub(string exeDir, string csvPath, string groupCol, string subCol, string probCol, double targetSum, string indexCol ,string globalIndexCol)
        {
            string[] lines;
            using (var fs = new FileStream(csvPath,
                                           FileMode.Open,
                                           FileAccess.Read,
                                           FileShare.ReadWrite))
            using (var sr = new StreamReader(fs))
            {
                var temp = new List<string>();
                string row;
                while ((row = sr.ReadLine()) != null)
                    temp.Add(row);
                lines = temp.ToArray();
            }
            int hdr = Array.FindIndex(lines, l => l.Contains(groupCol) && l.Contains(subCol) && l.Contains(probCol));
            if (hdr < 0)
            {
                Console.WriteLine($"[{Path.GetFileName(csvPath)}] 헤더({groupCol},{subCol},{probCol}) 못 찾음");
                return;
            }

            var headers = lines[hdr]
                .Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(h => h.Trim())
                .ToArray();

            int gi = Array.IndexOf(headers, groupCol);
            int si = Array.IndexOf(headers, subCol);
            int pi = Array.IndexOf(headers, probCol);
            int gii = -1, globii = -1;

            bool doGroupIndex = !string.IsNullOrEmpty(indexCol);
            bool doGlobalIndex = !string.IsNullOrEmpty(globalIndexCol);

        
            if (doGroupIndex)
            {
                gii = Array.IndexOf(headers, indexCol);
                if (gii < 0)
                {
                    Console.WriteLine($"[{Path.GetFileName(csvPath)}] 인덱스 컬럼 못 찾음: {indexCol}");
                    return;
                }
            }
            if (doGlobalIndex)
            {
                globii = Array.IndexOf(headers, globalIndexCol);
                if (globii < 0)
                {
                    Console.WriteLine($"[{Path.GetFileName(csvPath)}] 전체 인덱스 컬럼 못 찾음: {globalIndexCol}");
                    return;
                }
            }

            var sums = new Dictionary<(string grp, string sub), double>();
            var groupIndices = new Dictionary<string, HashSet<string>>();
            
            var firstGroupDup = new Dictionary<string, string>();
            var globalSeen = new HashSet<string>();
            string firstGlobalDup = null;

            for (int i = hdr + 1; i < lines.Length; i++)
            {
                if (lines[i].StartsWith(";")) continue;

                var cols = lines[i]
                    .Split(new[] { ',', ';', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToArray();
                if (cols.Length <= Math.Max(Math.Max(gi, si), pi)) continue;

                var g = cols[gi];
                var s = cols[si];
                if (string.IsNullOrEmpty(g) || string.IsNullOrEmpty(s)) continue;
                if (!double.TryParse(cols[pi], out var p)) continue;

                var key = (g, s);
                if (sums.ContainsKey(key)) sums[key] += p;
                else sums[key] = p;

                if (doGroupIndex && cols.Length > gii)
                {
                    var idx = cols[gii];
                    if (!groupIndices.ContainsKey(g))
                        groupIndices[g] = new HashSet<string>();
                    if (groupIndices[g].Contains(idx))
                    {
                        if (!firstGroupDup.ContainsKey(g))
                            firstGroupDup[g] = idx;
                    }
                    else groupIndices[g].Add(idx);
                }
                if (doGlobalIndex && cols.Length > globii)
                {
                    var gidx = cols[globii];
                    if (globalSeen.Contains(gidx))
                    {
                        if (firstGlobalDup == null) firstGlobalDup = gidx;
                    }
                    else globalSeen.Add(gidx);
                }
            }

            var outputDir = Path.Combine(exeDir, "output");
            Directory.CreateDirectory(outputDir);
            var outPath = Path.Combine(outputDir, Path.GetFileNameWithoutExtension(csvPath) + ".txt");


            using (var sw = new StreamWriter(outPath))
            {
                if (!string.IsNullOrEmpty(firstGlobalDup))
                    sw.WriteLine($"{globalIndexCol} 중복 INDEX: {firstGlobalDup}");
                foreach (var kv in sums)
                {
                    var err = new List<string>();
                    if (kv.Value != targetSum) err.Add("!!!!ERROR!!!!");
                    if (firstGroupDup.TryGetValue(kv.Key.grp, out var dupVal))
                        err.Add($"!!!!{indexCol} 중복 INDEX {dupVal}!!!!");
                    sw.WriteLine($"{groupCol} {kv.Key.grp}/  {subCol} {kv.Key.sub}서브그룹 -> {kv.Value} / {targetSum}"
                        + (err.Any() ? "\t" + string.Join(" ", err) : string.Empty));
                }
            }

            Console.WriteLine($"[output/{Path.GetFileName(outPath)}] 생성 (그룹+서브그룹):");
            foreach (var kv in sums)
            {
                var err = new List<string>();
                if (kv.Value != targetSum) err.Add("!!!!ERROR!!!!");
                if (firstGroupDup.TryGetValue(kv.Key.grp, out var dupVal))
                    err.Add($"!!!!DUP INDEX {dupVal}!!!!");
                Console.WriteLine($"  {kv.Key.grp}그룹 {kv.Key.sub}서브그룹 -> {kv.Value} / {targetSum}"
                    + (err.Any() ? "\t" + string.Join(" ", err) : string.Empty));
            }
            Console.WriteLine();
        }
    }
}
