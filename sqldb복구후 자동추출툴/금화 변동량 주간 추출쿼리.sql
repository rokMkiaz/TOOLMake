

    ;WITH Base AS
(
    SELECT
        idx,
        group_num,
        accunique,
        cname,
        balance,
        inpoint,
        outpoint,
        maketime,
        week_start = DATEADD(WEEK, DATEDIFF(WEEK, 0, maketime), 0)
    FROM csb_cash_WeekBak.[dbo].[TCash]
),
Ranked AS
(
    SELECT
        idx,
        group_num,
        accunique,
        cname,
        balance,
        inpoint,
        outpoint,
        maketime,
        week_start,
        ROW_NUMBER() OVER
        (
            PARTITION BY group_num, accunique, week_start
            ORDER BY maketime ASC, idx ASC
        ) AS rn_start,
        ROW_NUMBER() OVER
        (
            PARTITION BY group_num, accunique, week_start
            ORDER BY maketime DESC, idx DESC
        ) AS rn_end
    FROM Base
),
Weekly AS
(
    SELECT
        group_num,
        accunique,

        MAX(CASE WHEN rn_start = 1 THEN cname END) AS cname,

        week_start,

        MAX(CASE WHEN rn_start = 1 THEN maketime END) AS start_time,

        -- 시작 balance 보정
        MAX(CASE WHEN rn_start = 1
            THEN balance - inpoint + outpoint
        END) AS start_balance,

        MAX(CASE WHEN rn_end = 1 THEN maketime END) AS end_time,
        MAX(CASE WHEN rn_end = 1 THEN balance END)  AS end_balance

    FROM Ranked
    GROUP BY
        group_num,
        accunique,
        week_start
),
WeeklyTop100 AS
(
    SELECT *,
           ROW_NUMBER() OVER
           (
               PARTITION BY group_num, week_start
               ORDER BY end_balance DESC, accunique
           ) AS rn
    FROM Weekly
)
SELECT
    group_num,
    accunique,
    cname,
    week_start,
    DATEADD(DAY, 6, week_start) AS week_end,
    --start_time,
    start_balance,
    --end_time,
    end_balance,
    end_balance - start_balance AS balance_diff
FROM WeeklyTop100
WHERE rn <= 100 and week_start > '2025-01-06 00:00:00.000' and week_start < DATEADD(DAY, - (DATEPART(WEEKDAY, GETDATE()) + @@DATEFIRST - 2) % 7, CAST(GETDATE() AS DATE))
ORDER BY
    week_start DESC,
    end_balance desc

