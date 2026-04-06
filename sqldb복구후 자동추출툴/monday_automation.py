"""
금화조사 주간 자동화 스크립트
- 매주 월요일 실행
- FTP에서 해당 날짜의 csb_cash.bak 다운로드
- .sql 파일 날짜 자동 변경 후 실행
- 결과를 CSV로 저장
"""

import ftplib
import os
import re
import csv
import configparser
import pyodbc
from datetime import datetime, timedelta
import logging

# ── 로그 설정 ─────────────────────────────────────────────
LOG_DIR = r"D:\Work_1003bm\금화조사\logs"
os.makedirs(LOG_DIR, exist_ok=True)

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(message)s",
    handlers=[
        logging.FileHandler(
            os.path.join(LOG_DIR, f"automation_{datetime.now().strftime('%Y%m%d')}.log"),
            encoding="utf-8"
        ),
        logging.StreamHandler()
    ]
)
log = logging.getLogger(__name__)

# ── 경로 설정 ─────────────────────────────────────────────
BASE_DIR   = r"D:\Work_1003bm\금화조사"
CONFIG_FILE = os.path.join(BASE_DIR, "config.ini")
BAK_SAVE_DIR = BASE_DIR   # .bak 저장 위치
RESULT_DIR   = os.path.join(BASE_DIR, "결과")
os.makedirs(RESULT_DIR, exist_ok=True)


def get_this_monday() -> str:
    """이번 주 월요일 날짜를 YYYYMMDD 형식으로 반환"""
    today = datetime.now()
    monday = today - timedelta(days=today.weekday())  # weekday() 0=월요일
    return monday.strftime("%Y%m%d")


def load_config() -> configparser.ConfigParser:
    """config.ini 파일 읽기"""
    if not os.path.exists(CONFIG_FILE):
        raise FileNotFoundError(
            f"설정 파일이 없습니다: {CONFIG_FILE}\n"
            "config.ini 파일을 생성하고 FTP/DB 정보를 입력해주세요."
        )
    cfg = configparser.ConfigParser()
    cfg.read(CONFIG_FILE, encoding="utf-8")
    return cfg


def download_bak(cfg: configparser.ConfigParser, date_str: str) -> str:
    """FTP에서 YYYYMMDD/csb_cash.bak 다운로드 → 저장 경로 반환"""
    host = cfg["FTP"]["host"]
    port = int(cfg["FTP"].get("port", 21))
    user = cfg["FTP"]["user"]
    password = cfg["FTP"]["password"]
    ftp_base = cfg["FTP"].get("base_dir", "")   # 서버 내 기본 경로 (없으면 루트)

    remote_path = f"{ftp_base}/{date_str}/csb_cash.bak".lstrip("/")
    local_path  = os.path.join(BAK_SAVE_DIR, f"csb_cash_{date_str}.bak")

    log.info(f"FTP 접속: {host}:{port}")
    with ftplib.FTP() as ftp:
        ftp.connect(host, port, timeout=60)
        ftp.login(user, password)
        log.info(f"FTP 로그인 성공, 다운로드: {remote_path}")
        with open(local_path, "wb") as f:
            ftp.retrbinary(f"RETR {remote_path}", f.write)

    log.info(f"다운로드 완료: {local_path}")
    return local_path


def get_sql_files() -> list:
    """금화조사 폴더 내 .sql 파일 목록 반환"""
    sql_files = [
        os.path.join(BASE_DIR, f)
        for f in os.listdir(BASE_DIR)
        if f.lower().endswith(".sql")
    ]
    if not sql_files:
        raise FileNotFoundError(f"{BASE_DIR} 폴더에 .sql 파일이 없습니다.")
    return sorted(sql_files)


def update_sql_date(sql_path: str, date_str: str) -> str:
    """
    .sql 파일 내 날짜 패턴 '20XXXXXX' 을 date_str 로 교체한
    임시 SQL 문자열 반환 (원본 파일 수정 없음)
    """
    # 인코딩 자동 감지: UTF-8 → CP949(EUC-KR) 순서로 시도
    for enc in ("utf-8-sig", "cp949", "utf-8"):
        try:
            with open(sql_path, "r", encoding=enc) as f:
                sql = f.read()
            log.info(f"SQL 파일 인코딩: {enc}")
            break
        except UnicodeDecodeError:
            continue
    else:
        raise UnicodeDecodeError(f"SQL 파일 인코딩을 읽을 수 없습니다: {sql_path}")

    # '20XXXXXX' 형식의 8자리 날짜 문자열을 새 날짜로 교체
    # 예: '20260310' → '20260317'
    updated = re.sub(r"'20\d{6}'", f"'{date_str}'", sql)
    log.info(f"날짜 교체 완료: {sql_path} → '{date_str}'")
    return updated


def restore_database(cfg: configparser.ConfigParser, bak_path: str):
    """master DB에 접속해 .bak 파일을 csb_cash DB로 복원"""
    server   = cfg["DATABASE"]["server"]
    database = cfg["DATABASE"].get("restore_db", "csb_cash")

    # master DB로 접속 (복원 명령은 master에서 실행)
    conn_str = (
        f"DRIVER={{ODBC Driver 17 for SQL Server}};"
        f"SERVER={server};"
        f"DATABASE=master;"
        f"Trusted_Connection=yes;"
    )

    log.info(f"DB 복원 시작: {bak_path} → [{database}]")
    conn = pyodbc.connect(conn_str, timeout=30, autocommit=True)
    try:
        cursor = conn.cursor()

        # SQL Server 기본 데이터/로그 경로 조회
        cursor.execute("SELECT SERVERPROPERTY('InstanceDefaultDataPath'), SERVERPROPERTY('InstanceDefaultLogPath')")
        row = cursor.fetchone()
        data_path = str(row[0]).rstrip("\\")
        log_path  = str(row[1]).rstrip("\\")
        log.info(f"SQL Server 데이터 경로: {data_path}")
        log.info(f"SQL Server 로그 경로:   {log_path}")

        # .bak 내 논리 파일명 조회
        cursor.execute(f"RESTORE FILELISTONLY FROM DISK = N'{bak_path}'")
        file_rows = cursor.fetchall()
        move_clauses = []
        for fr in file_rows:
            logical_name = fr[0]
            file_type    = fr[2].strip().upper()   # 'D'=데이터, 'L'=로그
            if file_type == "L":
                dest = f"{log_path}\\{database}_log.ldf"
            else:
                dest = f"{data_path}\\{database}.mdf"
            move_clauses.append(f"MOVE N'{logical_name}' TO N'{dest}'")
            log.info(f"  MOVE {logical_name} → {dest}")

        move_sql = ",\n    ".join(move_clauses)
        restore_sql = f"""
        RESTORE DATABASE [{database}]
        FROM DISK = N'{bak_path}'
        WITH REPLACE, RECOVERY,
        {move_sql}
        """

        cursor.execute(restore_sql)
        while cursor.nextset():
            pass
        log.info(f"DB 복원 완료: [{database}]")
    finally:
        conn.close()

    return database


def drop_database(cfg: configparser.ConfigParser):
    """CSV 추출 완료 후 복원했던 DB 삭제"""
    server   = cfg["DATABASE"]["server"]
    database = cfg["DATABASE"].get("restore_db", "csb_cash_WeekBak")

    conn_str = (
        f"DRIVER={{ODBC Driver 17 for SQL Server}};"
        f"SERVER={server};"
        f"DATABASE=master;"
        f"Trusted_Connection=yes;"
    )

    drop_sql = f"""
    IF EXISTS (SELECT name FROM sys.databases WHERE name = N'{database}')
    BEGIN
        ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
        DROP DATABASE [{database}];
    END
    """

    log.info(f"DB 삭제 시작: [{database}]")
    conn = pyodbc.connect(conn_str, timeout=30, autocommit=True)
    try:
        cursor = conn.cursor()
        cursor.execute(drop_sql)
        log.info(f"DB 삭제 완료: [{database}]")
    finally:
        conn.close()


def run_sql_and_save_csv(cfg: configparser.ConfigParser, sql: str, date_str: str, sql_filename: str):
    """SQL 실행 후 결과를 CSV로 저장"""
    server   = cfg["DATABASE"]["server"]
    database = cfg["DATABASE"].get("restore_db", "csb_cash")

    conn_str = (
        f"DRIVER={{ODBC Driver 17 for SQL Server}};"
        f"SERVER={server};"
        f"DATABASE={database};"
        f"Trusted_Connection=yes;"   # Windows 인증
    )

    log.info(f"DB 접속: {server} / {database}")
    with pyodbc.connect(conn_str, timeout=30) as conn:
        cursor = conn.cursor()
        cursor.execute(sql)
        columns = [col[0] for col in cursor.description]
        rows = cursor.fetchall()

    base_name = os.path.splitext(os.path.basename(sql_filename))[0]
    csv_path = os.path.join(RESULT_DIR, f"{base_name}_{date_str}.csv")

    with open(csv_path, "w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(columns)
        writer.writerows(rows)

    log.info(f"CSV 저장 완료: {csv_path}  ({len(rows)}행)")
    return csv_path


def main():
    log.info("=" * 50)
    log.info("금화조사 주간 자동화 시작")
    date_str = get_this_monday()
    log.info(f"처리 날짜: {date_str}")

    try:
        cfg = load_config()

        # 1. FTP 다운로드
        bak_path = download_bak(cfg, date_str)
        log.info(f"[1/5] .bak 다운로드 완료: {bak_path}")

        # 2. DB 복원
        restore_database(cfg, bak_path)
        log.info(f"[2/5] DB 복원 완료")

        # 3. SQL 파일 실행
        sql_files = get_sql_files()
        log.info(f"[3/5] SQL 파일 {len(sql_files)}개 발견: {[os.path.basename(f) for f in sql_files]}")

        for sql_file in sql_files:
            updated_sql = update_sql_date(sql_file, date_str)
            csv_path = run_sql_and_save_csv(cfg, updated_sql, date_str, sql_file)
            log.info(f"[4/5] 결과 저장: {csv_path}")

        # 4. 복원 DB 삭제
        drop_database(cfg)
        log.info(f"[5/5] DB 삭제 완료")

        log.info("자동화 완료!")

    except Exception as e:
        log.error(f"오류 발생: {e}", exc_info=True)
        raise


if __name__ == "__main__":
    main()