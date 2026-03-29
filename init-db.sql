-- ============================================
-- Ceremony Database Initialization Script
-- Generated from: D:\appsystems\Ceremony\Ceremony.Models\Model1.edmx
-- ============================================

-- Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'Ceremony')
BEGIN
    CREATE DATABASE [Ceremony];
END
GO

USE [Ceremony];
GO

-- ============================================
-- 1. Tables (order matters for FK constraints)
-- ============================================

-- 1.1 Admins
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Admins]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Admins] (
        [AdminID]    INT            IDENTITY(1,1) NOT NULL,
        [Name]       NVARCHAR(50)   NULL,
        [Username]   NVARCHAR(50)   NOT NULL,
        [Password]   NVARCHAR(20)   NOT NULL,
        [IsEnabled]  BIT            NOT NULL,
        CONSTRAINT [PK_Admins] PRIMARY KEY CLUSTERED ([AdminID] ASC),
        CONSTRAINT [UQ_Admins_Username] UNIQUE ([Username])
    );
END
GO

-- 1.2 Zipcodes
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Zipcodes]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Zipcodes] (
        [ZipcodeID]  INT            NOT NULL,
        [CountryID]  INT            NOT NULL,
        [City]       NVARCHAR(10)   NOT NULL,
        [Area]       NVARCHAR(10)   NOT NULL,
        [Zipcode]    NVARCHAR(10)   NOT NULL,
        [IsDisplay]  INT            NOT NULL,
        CONSTRAINT [PK_Zipcodes] PRIMARY KEY CLUSTERED ([ZipcodeID] ASC)
    );
END
GO

-- 1.3 Believers
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Believers]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Believers] (
        [BelieverID]       UNIQUEIDENTIFIER NOT NULL,
        [EmployeeType]     INT              NOT NULL,
        [HallName]         NVARCHAR(10)     NULL,
        [Name]             NVARCHAR(30)     NOT NULL,
        [Phone]            NVARCHAR(30)     NULL,
        [MailZipcodeID]    INT              NULL,
        [MailZipcode]      NVARCHAR(10)     NULL,
        [MailAddress]      NVARCHAR(250)    NULL,
        [TextZipcodeID]    INT              NULL,
        [TextZipcode]      NVARCHAR(10)     NULL,
        [TextAddress]      NVARCHAR(250)    NULL,
        [LivingNameOne]    NVARCHAR(30)     NULL,
        [LivingNameTwo]    NVARCHAR(30)     NULL,
        [LivingNameThree]  NVARCHAR(30)     NULL,
        [LivingNameFour]   NVARCHAR(30)     NULL,
        [LivingNameFive]   NVARCHAR(30)     NULL,
        [LivingNameSix]    NVARCHAR(30)     NULL,
        [DeadNameOne]      NVARCHAR(30)     NULL,
        [DeadNameTwo]      NVARCHAR(30)     NULL,
        [DeadNameThree]    NVARCHAR(30)     NULL,
        [DeadNameFour]     NVARCHAR(30)     NULL,
        [DeadNameFive]     NVARCHAR(30)     NULL,
        [DeadNameSix]      NVARCHAR(30)     NULL,
        [IsFixedNumber]    BIT              NOT NULL,
        CONSTRAINT [PK_Believers] PRIMARY KEY CLUSTERED ([BelieverID] ASC)
    );
END
GO

-- 1.4 CeremonyCategorys
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[CeremonyCategorys]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[CeremonyCategorys] (
        [CeremonyCategoryID]  UNIQUEIDENTIFIER NOT NULL,
        [Title]               NVARCHAR(50)     NOT NULL,
        [ParentID]            UNIQUEIDENTIFIER NULL,
        [Sort]                INT              NOT NULL,
        CONSTRAINT [PK_CeremonyCategorys] PRIMARY KEY CLUSTERED ([CeremonyCategoryID] ASC)
    );
END
GO

-- 1.5 Signups
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Signups]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[Signups] (
        [SignupID]                  UNIQUEIDENTIFIER NOT NULL,
        [Year]                     INT              NOT NULL,
        [CeremonyCategoryID]       UNIQUEIDENTIFIER NOT NULL,
        [CeremonyDate]             DATETIME         NULL,
        [SignupType]               INT              NOT NULL,
        [BelieverID]               UNIQUEIDENTIFIER NULL,
        [NumberTitle]              NVARCHAR(5)      NULL,
        [Number]                   INT              NULL,
        [Fee]                      INT              NULL,
        [Name]                     NVARCHAR(30)     NULL,
        [Phone]                    NVARCHAR(30)     NULL,
        [LivingNameOne]            NVARCHAR(30)     NULL,
        [LivingNameTwo]            NVARCHAR(30)     NULL,
        [LivingNameThree]          NVARCHAR(30)     NULL,
        [LivingNameFour]           NVARCHAR(30)     NULL,
        [LivingNameFive]           NVARCHAR(30)     NULL,
        [LivingNameSix]            NVARCHAR(30)     NULL,
        [DeadNameOne]              NVARCHAR(30)     NULL,
        [DeadNameTwo]              NVARCHAR(30)     NULL,
        [DeadNameThree]            NVARCHAR(30)     NULL,
        [DeadNameFour]             NVARCHAR(30)     NULL,
        [DeadNameFive]             NVARCHAR(30)     NULL,
        [DeadNameSix]              NVARCHAR(30)     NULL,
        [MailZipcodeID]            INT              NULL,
        [MailZipcode]              NVARCHAR(10)     NULL,
        [MailAddress]              NVARCHAR(250)    NULL,
        [TextZipcodeID]            INT              NULL,
        [TextZipcode]              NVARCHAR(10)     NULL,
        [TextAddress]              NVARCHAR(250)    NULL,
        [Remark]                   NVARCHAR(250)    NULL,
        [PrepayYear]               INT              NULL,
        [PrepayCeremonyCategoryID] UNIQUEIDENTIFIER NULL,
        [AdminID]                  INT              NOT NULL,
        [Createdate]               DATETIME         NOT NULL,
        CONSTRAINT [PK_Signups] PRIMARY KEY CLUSTERED ([SignupID] ASC)
    );
END
GO

-- 1.6 SignupLogs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[SignupLogs]') AND type = 'U')
BEGIN
    CREATE TABLE [dbo].[SignupLogs] (
        [SignupLogID]                    UNIQUEIDENTIFIER NOT NULL,
        [SignupID]                       UNIQUEIDENTIFIER NOT NULL,
        [Year]                           INT              NOT NULL,
        [CeremonyCategoryTitle]          NVARCHAR(50)     NOT NULL,
        [SignupType]                     INT              NOT NULL,
        [HallName]                       NVARCHAR(10)     NULL,
        [Name]                           NVARCHAR(30)     NOT NULL,
        [Phone]                          NVARCHAR(30)     NULL,
        [NumberTitle]                    NVARCHAR(5)      NULL,
        [Number]                         INT              NULL,
        [Fee]                            INT              NULL,
        [LivingNameOne]                  NVARCHAR(30)     NULL,
        [LivingNameTwo]                  NVARCHAR(30)     NULL,
        [LivingNameThree]               NVARCHAR(30)     NULL,
        [LivingNameFour]                NVARCHAR(30)     NULL,
        [LivingNameFive]                NVARCHAR(30)     NULL,
        [LivingNameSix]                 NVARCHAR(30)     NULL,
        [DeadNameOne]                    NVARCHAR(30)     NULL,
        [DeadNameTwo]                    NVARCHAR(30)     NULL,
        [DeadNameThree]                  NVARCHAR(30)     NULL,
        [DeadNameFour]                   NVARCHAR(30)     NULL,
        [DeadNameFive]                   NVARCHAR(30)     NULL,
        [DeadNameSix]                    NVARCHAR(30)     NULL,
        [MailCity]                        NVARCHAR(50)     NULL,
        [MailZone]                        NVARCHAR(50)     NULL,
        [MailAddress]                     NVARCHAR(250)    NULL,
        [TextCity]                        NVARCHAR(50)     NULL,
        [TextZone]                        NVARCHAR(50)     NULL,
        [TextAddress]                     NVARCHAR(250)    NULL,
        [Remark]                          NVARCHAR(250)    NULL,
        [PrepayYear]                      INT              NULL,
        [PrepayCeremonyCategoryTitle]     NVARCHAR(50)     NULL,
        [Admin]                           NVARCHAR(50)     NOT NULL,
        [Createdate]                      DATETIME         NOT NULL,
        CONSTRAINT [PK_SignupLogs] PRIMARY KEY CLUSTERED ([SignupLogID] ASC)
    );
END
GO

-- ============================================
-- 2. Foreign Key Constraints
-- ============================================

-- FK: Believers.MailZipcodeID -> Zipcodes.ZipcodeID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Believers_Zipcodes')
BEGIN
    ALTER TABLE [dbo].[Believers]
        ADD CONSTRAINT [FK_Believers_Zipcodes]
        FOREIGN KEY ([MailZipcodeID]) REFERENCES [dbo].[Zipcodes] ([ZipcodeID]);
END
GO

-- FK: Believers.TextZipcodeID -> Zipcodes.ZipcodeID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Believers_Zipcodes1')
BEGIN
    ALTER TABLE [dbo].[Believers]
        ADD CONSTRAINT [FK_Believers_Zipcodes1]
        FOREIGN KEY ([TextZipcodeID]) REFERENCES [dbo].[Zipcodes] ([ZipcodeID]);
END
GO

-- FK: CeremonyCategorys.ParentID -> CeremonyCategorys.CeremonyCategoryID (self-referencing)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_CeremonyCategorys_CeremonyCategorys1')
BEGIN
    ALTER TABLE [dbo].[CeremonyCategorys]
        ADD CONSTRAINT [FK_CeremonyCategorys_CeremonyCategorys1]
        FOREIGN KEY ([ParentID]) REFERENCES [dbo].[CeremonyCategorys] ([CeremonyCategoryID]);
END
GO

-- FK: Signups.BelieverID -> Believers.BelieverID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Signups_Believers')
BEGIN
    ALTER TABLE [dbo].[Signups]
        ADD CONSTRAINT [FK_Signups_Believers]
        FOREIGN KEY ([BelieverID]) REFERENCES [dbo].[Believers] ([BelieverID]);
END
GO

-- FK: Signups.CeremonyCategoryID -> CeremonyCategorys.CeremonyCategoryID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Signups_CeremonyCategorys')
BEGIN
    ALTER TABLE [dbo].[Signups]
        ADD CONSTRAINT [FK_Signups_CeremonyCategorys]
        FOREIGN KEY ([CeremonyCategoryID]) REFERENCES [dbo].[CeremonyCategorys] ([CeremonyCategoryID]);
END
GO

-- FK: Signups.PrepayCeremonyCategoryID -> CeremonyCategorys.CeremonyCategoryID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Signups_CeremonyCategorys2')
BEGIN
    ALTER TABLE [dbo].[Signups]
        ADD CONSTRAINT [FK_Signups_CeremonyCategorys2]
        FOREIGN KEY ([PrepayCeremonyCategoryID]) REFERENCES [dbo].[CeremonyCategorys] ([CeremonyCategoryID]);
END
GO

-- FK: Signups.MailZipcodeID -> Zipcodes.ZipcodeID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Signups_Zipcodes')
BEGIN
    ALTER TABLE [dbo].[Signups]
        ADD CONSTRAINT [FK_Signups_Zipcodes]
        FOREIGN KEY ([MailZipcodeID]) REFERENCES [dbo].[Zipcodes] ([ZipcodeID]);
END
GO

-- FK: Signups.TextZipcodeID -> Zipcodes.ZipcodeID
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Signups_Zipcodes1')
BEGIN
    ALTER TABLE [dbo].[Signups]
        ADD CONSTRAINT [FK_Signups_Zipcodes1]
        FOREIGN KEY ([TextZipcodeID]) REFERENCES [dbo].[Zipcodes] ([ZipcodeID]);
END
GO

-- ============================================
-- 3. Views
-- ============================================

-- 3.1 BelieverView
-- Joins: Believers + Signups + CeremonyCategorys + Zipcodes
IF EXISTS (SELECT * FROM sys.views WHERE name = 'BelieverView')
    DROP VIEW [dbo].[BelieverView];
GO

CREATE VIEW [dbo].[BelieverView]
AS
SELECT
    b.[BelieverID],
    b.[HallName],
    b.[Name],
    b.[Phone],
    mz.[City]       AS [MailCity],
    mz.[Area]       AS [MailZone],
    b.[MailAddress],
    tz.[City]       AS [TextCity],
    tz.[Area]       AS [TextZone],
    b.[TextAddress],
    s.[SignupID],
    s.[Year],
    cc.[Title]      AS [CeremonyTitle],
    cc.[Sort]       AS [CeremonySort],
    s.[SignupType],
    s.[CeremonyCategoryID],
    s.[NumberTitle],
    s.[Number],
    s.[Fee],
    s.[DeadNameOne],
    s.[DeadNameTwo],
    s.[DeadNameThree],
    s.[DeadNameFour],
    s.[DeadNameFive],
    s.[DeadNameSix],
    s.[LivingNameOne],
    s.[LivingNameTwo],
    s.[LivingNameThree],
    s.[LivingNameFour],
    s.[LivingNameFive],
    s.[LivingNameSix],
    s.[PrepayYear],
    pcc.[Title]     AS [PrepayCeremonyTitle],
    s.[Remark],
    s.[PrepayCeremonyCategoryID]
FROM [dbo].[Believers] b
LEFT JOIN [dbo].[Signups] s ON b.[BelieverID] = s.[BelieverID]
LEFT JOIN [dbo].[CeremonyCategorys] cc ON s.[CeremonyCategoryID] = cc.[CeremonyCategoryID]
LEFT JOIN [dbo].[CeremonyCategorys] pcc ON s.[PrepayCeremonyCategoryID] = pcc.[CeremonyCategoryID]
LEFT JOIN [dbo].[Zipcodes] mz ON b.[MailZipcodeID] = mz.[ZipcodeID]
LEFT JOIN [dbo].[Zipcodes] tz ON b.[TextZipcodeID] = tz.[ZipcodeID];
GO

-- 3.2 SignupView
-- Joins: Signups + Believers + CeremonyCategorys + Zipcodes + Admins
IF EXISTS (SELECT * FROM sys.views WHERE name = 'SignupView')
    DROP VIEW [dbo].[SignupView];
GO

CREATE VIEW [dbo].[SignupView]
AS
SELECT
    s.[SignupID],
    s.[BelieverID],
    s.[Year],
    cc.[Title]      AS [CeremonyTitle],
    cc.[Sort]       AS [CeremonySort],
    s.[SignupType],
    s.[CeremonyCategoryID],
    s.[NumberTitle],
    s.[Number],
    s.[Fee],
    CASE b.[EmployeeType]
        WHEN 1 THEN N'志工'
        WHEN 2 THEN N'員工'
        WHEN 3 THEN N'委員'
        WHEN 4 THEN N'執事'
        WHEN 5 THEN N'常住'
        ELSE CAST(b.[EmployeeType] AS VARCHAR(6))
    END             AS [Employee],
    s.[Name],
    s.[Remark],
    b.[HallName],
    s.[DeadNameOne],
    s.[DeadNameTwo],
    s.[DeadNameThree],
    s.[DeadNameFour],
    s.[DeadNameFive],
    s.[DeadNameSix],
    s.[LivingNameOne],
    s.[LivingNameTwo],
    s.[LivingNameThree],
    s.[LivingNameFour],
    s.[LivingNameFive],
    s.[LivingNameSix],
    s.[PrepayYear],
    s.[PrepayCeremonyCategoryID],
    pcc.[Title]     AS [PrepayCeremonyTitle],
    s.[Phone],
    s.[MailZipcode],
    mz.[City]       AS [MailCity],
    mz.[Area]       AS [MailZone],
    s.[MailAddress],
    s.[TextZipcode],
    tz.[City]       AS [TextCity],
    tz.[Area]       AS [TextZone],
    s.[TextAddress],
    a.[Name]        AS [AdminName],
    s.[Createdate],
    b.[IsFixedNumber]
FROM [dbo].[Signups] s
LEFT JOIN [dbo].[Believers] b ON s.[BelieverID] = b.[BelieverID]
LEFT JOIN [dbo].[CeremonyCategorys] cc ON s.[CeremonyCategoryID] = cc.[CeremonyCategoryID]
LEFT JOIN [dbo].[CeremonyCategorys] pcc ON s.[PrepayCeremonyCategoryID] = pcc.[CeremonyCategoryID]
LEFT JOIN [dbo].[Zipcodes] mz ON s.[MailZipcodeID] = mz.[ZipcodeID]
LEFT JOIN [dbo].[Zipcodes] tz ON s.[TextZipcodeID] = tz.[ZipcodeID]
LEFT JOIN [dbo].[Admins] a ON s.[AdminID] = a.[AdminID];
GO

-- ============================================
-- 4. Seed Data
-- ============================================

-- 4.1 Default Admin
IF NOT EXISTS (SELECT 1 FROM [dbo].[Admins] WHERE [Username] = N'admin')
BEGIN
    INSERT INTO [dbo].[Admins] ([Name], [Username], [Password], [IsEnabled])
    VALUES (N'系統管理員', N'admin', N'admin', 1);
END
GO

-- 4.2 Taiwan Zipcodes
IF NOT EXISTS (SELECT 1 FROM [dbo].[Zipcodes])
BEGIN
    INSERT INTO [dbo].[Zipcodes] ([ZipcodeID], [CountryID], [City], [Area], [Zipcode], [IsDisplay]) VALUES
    -- 台北市
    (1, 1, N'台北市', N'中正區', N'100', 1),
    (2, 1, N'台北市', N'大同區', N'103', 1),
    (3, 1, N'台北市', N'中山區', N'104', 1),
    (4, 1, N'台北市', N'松山區', N'105', 1),
    (5, 1, N'台北市', N'大安區', N'106', 1),
    (6, 1, N'台北市', N'萬華區', N'108', 1),
    (7, 1, N'台北市', N'信義區', N'110', 1),
    (8, 1, N'台北市', N'士林區', N'111', 1),
    (9, 1, N'台北市', N'北投區', N'112', 1),
    (10, 1, N'台北市', N'內湖區', N'114', 1),
    (11, 1, N'台北市', N'南港區', N'115', 1),
    (12, 1, N'台北市', N'文山區', N'116', 1),
    -- 基隆市
    (13, 1, N'基隆市', N'仁愛區', N'200', 1),
    (14, 1, N'基隆市', N'信義區', N'201', 1),
    (15, 1, N'基隆市', N'中正區', N'202', 1),
    (16, 1, N'基隆市', N'中山區', N'203', 1),
    (17, 1, N'基隆市', N'安樂區', N'204', 1),
    (18, 1, N'基隆市', N'暖暖區', N'205', 1),
    (19, 1, N'基隆市', N'七堵區', N'206', 1),
    -- 新北市
    (20, 1, N'新北市', N'萬里區', N'207', 1),
    (21, 1, N'新北市', N'金山區', N'208', 1),
    (22, 1, N'新北市', N'板橋區', N'220', 1),
    (23, 1, N'新北市', N'汐止區', N'221', 1),
    (24, 1, N'新北市', N'深坑區', N'222', 1),
    (25, 1, N'新北市', N'石碇區', N'223', 1),
    (26, 1, N'新北市', N'瑞芳區', N'224', 1),
    (27, 1, N'新北市', N'平溪區', N'226', 1),
    (28, 1, N'新北市', N'雙溪區', N'227', 1),
    (29, 1, N'新北市', N'貢寮區', N'228', 1),
    (30, 1, N'新北市', N'新店區', N'231', 1),
    (31, 1, N'新北市', N'坪林區', N'232', 1),
    (32, 1, N'新北市', N'烏來區', N'233', 1),
    (33, 1, N'新北市', N'永和區', N'234', 1),
    (34, 1, N'新北市', N'中和區', N'235', 1),
    (35, 1, N'新北市', N'土城區', N'236', 1),
    (36, 1, N'新北市', N'三峽區', N'237', 1),
    (37, 1, N'新北市', N'樹林區', N'238', 1),
    (38, 1, N'新北市', N'鶯歌區', N'239', 1),
    (39, 1, N'新北市', N'三重區', N'241', 1),
    (40, 1, N'新北市', N'新莊區', N'242', 1),
    (41, 1, N'新北市', N'泰山區', N'243', 1),
    (42, 1, N'新北市', N'林口區', N'244', 1),
    (43, 1, N'新北市', N'蘆洲區', N'247', 1),
    (44, 1, N'新北市', N'五股區', N'248', 1),
    (45, 1, N'新北市', N'八里區', N'249', 1),
    (46, 1, N'新北市', N'淡水區', N'251', 1),
    (47, 1, N'新北市', N'三芝區', N'252', 1),
    (48, 1, N'新北市', N'石門區', N'253', 1),
    -- 桃園市
    (49, 1, N'桃園市', N'中壢區', N'320', 1),
    (50, 1, N'桃園市', N'平鎮區', N'324', 1),
    (51, 1, N'桃園市', N'龍潭區', N'325', 1),
    (52, 1, N'桃園市', N'楊梅區', N'326', 1),
    (53, 1, N'桃園市', N'新屋區', N'327', 1),
    (54, 1, N'桃園市', N'觀音區', N'328', 1),
    (55, 1, N'桃園市', N'桃園區', N'330', 1),
    (56, 1, N'桃園市', N'龜山區', N'333', 1),
    (57, 1, N'桃園市', N'八德區', N'334', 1),
    (58, 1, N'桃園市', N'大溪區', N'335', 1),
    (59, 1, N'桃園市', N'復興區', N'336', 1),
    (60, 1, N'桃園市', N'大園區', N'337', 1),
    (61, 1, N'桃園市', N'蘆竹區', N'338', 1),
    -- 新竹
    (62, 1, N'新竹市', N'東區', N'300', 1),
    (63, 1, N'新竹市', N'北區', N'300', 1),
    (64, 1, N'新竹市', N'香山區', N'300', 1),
    (65, 1, N'新竹縣', N'竹北市', N'302', 1),
    (66, 1, N'新竹縣', N'湖口鄉', N'303', 1),
    (67, 1, N'新竹縣', N'新豐鄉', N'304', 1),
    (68, 1, N'新竹縣', N'新埔鎮', N'305', 1),
    (69, 1, N'新竹縣', N'關西鎮', N'306', 1),
    (70, 1, N'新竹縣', N'芎林鄉', N'307', 1),
    (71, 1, N'新竹縣', N'寶山鄉', N'308', 1),
    (72, 1, N'新竹縣', N'竹東鎮', N'310', 1),
    (73, 1, N'新竹縣', N'五峰鄉', N'311', 1),
    (74, 1, N'新竹縣', N'橫山鄉', N'312', 1),
    (75, 1, N'新竹縣', N'尖石鄉', N'313', 1),
    (76, 1, N'新竹縣', N'北埔鄉', N'314', 1),
    (77, 1, N'新竹縣', N'峨眉鄉', N'315', 1),
    -- 苗栗縣
    (78, 1, N'苗栗縣', N'竹南鎮', N'350', 1),
    (79, 1, N'苗栗縣', N'頭份市', N'351', 1),
    (80, 1, N'苗栗縣', N'三灣鄉', N'352', 1),
    (81, 1, N'苗栗縣', N'南庄鄉', N'353', 1),
    (82, 1, N'苗栗縣', N'獅潭鄉', N'354', 1),
    (83, 1, N'苗栗縣', N'後龍鎮', N'356', 1),
    (84, 1, N'苗栗縣', N'通霄鎮', N'357', 1),
    (85, 1, N'苗栗縣', N'苑裡鎮', N'358', 1),
    (86, 1, N'苗栗縣', N'苗栗市', N'360', 1),
    (87, 1, N'苗栗縣', N'造橋鄉', N'361', 1),
    (88, 1, N'苗栗縣', N'頭屋鄉', N'362', 1),
    (89, 1, N'苗栗縣', N'公館鄉', N'363', 1),
    (90, 1, N'苗栗縣', N'大湖鄉', N'364', 1),
    (91, 1, N'苗栗縣', N'泰安鄉', N'365', 1),
    (92, 1, N'苗栗縣', N'銅鑼鄉', N'366', 1),
    (93, 1, N'苗栗縣', N'三義鄉', N'367', 1),
    (94, 1, N'苗栗縣', N'西湖鄉', N'368', 1),
    (95, 1, N'苗栗縣', N'卓蘭鎮', N'369', 1),
    -- 台中市
    (96, 1, N'台中市', N'中區', N'400', 1),
    (97, 1, N'台中市', N'東區', N'401', 1),
    (98, 1, N'台中市', N'南區', N'402', 1),
    (99, 1, N'台中市', N'西區', N'403', 1),
    (100, 1, N'台中市', N'北區', N'404', 1),
    (101, 1, N'台中市', N'北屯區', N'406', 1),
    (102, 1, N'台中市', N'西屯區', N'407', 1),
    (103, 1, N'台中市', N'南屯區', N'408', 1),
    (104, 1, N'台中市', N'太平區', N'411', 1),
    (105, 1, N'台中市', N'大里區', N'412', 1),
    (106, 1, N'台中市', N'霧峰區', N'413', 1),
    (107, 1, N'台中市', N'烏日區', N'414', 1),
    (108, 1, N'台中市', N'豐原區', N'420', 1),
    (109, 1, N'台中市', N'后里區', N'421', 1),
    (110, 1, N'台中市', N'石岡區', N'422', 1),
    (111, 1, N'台中市', N'東勢區', N'423', 1),
    (112, 1, N'台中市', N'和平區', N'424', 1),
    (113, 1, N'台中市', N'新社區', N'426', 1),
    (114, 1, N'台中市', N'潭子區', N'427', 1),
    (115, 1, N'台中市', N'大雅區', N'428', 1),
    (116, 1, N'台中市', N'神岡區', N'429', 1),
    (117, 1, N'台中市', N'大肚區', N'432', 1),
    (118, 1, N'台中市', N'沙鹿區', N'433', 1),
    (119, 1, N'台中市', N'龍井區', N'434', 1),
    (120, 1, N'台中市', N'梧棲區', N'435', 1),
    (121, 1, N'台中市', N'清水區', N'436', 1),
    (122, 1, N'台中市', N'大甲區', N'437', 1),
    (123, 1, N'台中市', N'外埔區', N'438', 1),
    (124, 1, N'台中市', N'大安區', N'439', 1),
    -- 彰化縣
    (125, 1, N'彰化縣', N'彰化市', N'500', 1),
    (126, 1, N'彰化縣', N'芬園鄉', N'502', 1),
    (127, 1, N'彰化縣', N'花壇鄉', N'503', 1),
    (128, 1, N'彰化縣', N'秀水鄉', N'504', 1),
    (129, 1, N'彰化縣', N'鹿港鎮', N'505', 1),
    (130, 1, N'彰化縣', N'福興鄉', N'506', 1),
    (131, 1, N'彰化縣', N'線西鄉', N'507', 1),
    (132, 1, N'彰化縣', N'和美鎮', N'508', 1),
    (133, 1, N'彰化縣', N'伸港鄉', N'509', 1),
    (134, 1, N'彰化縣', N'員林市', N'510', 1),
    (135, 1, N'彰化縣', N'社頭鄉', N'511', 1),
    (136, 1, N'彰化縣', N'永靖鄉', N'512', 1),
    (137, 1, N'彰化縣', N'埔心鄉', N'513', 1),
    (138, 1, N'彰化縣', N'溪湖鎮', N'514', 1),
    (139, 1, N'彰化縣', N'大村鄉', N'515', 1),
    (140, 1, N'彰化縣', N'埔鹽鄉', N'516', 1),
    (141, 1, N'彰化縣', N'田中鎮', N'520', 1),
    (142, 1, N'彰化縣', N'北斗鎮', N'521', 1),
    (143, 1, N'彰化縣', N'田尾鄉', N'522', 1),
    (144, 1, N'彰化縣', N'埤頭鄉', N'523', 1),
    (145, 1, N'彰化縣', N'溪州鄉', N'524', 1),
    (146, 1, N'彰化縣', N'竹塘鄉', N'525', 1),
    (147, 1, N'彰化縣', N'二林鎮', N'526', 1),
    (148, 1, N'彰化縣', N'大城鄉', N'527', 1),
    (149, 1, N'彰化縣', N'芳苑鄉', N'528', 1),
    (150, 1, N'彰化縣', N'二水鄉', N'530', 1),
    -- 南投縣
    (151, 1, N'南投縣', N'南投市', N'540', 1),
    (152, 1, N'南投縣', N'中寮鄉', N'541', 1),
    (153, 1, N'南投縣', N'草屯鎮', N'542', 1),
    (154, 1, N'南投縣', N'國姓鄉', N'544', 1),
    (155, 1, N'南投縣', N'埔里鎮', N'545', 1),
    (156, 1, N'南投縣', N'仁愛鄉', N'546', 1),
    (157, 1, N'南投縣', N'名間鄉', N'551', 1),
    (158, 1, N'南投縣', N'集集鎮', N'552', 1),
    (159, 1, N'南投縣', N'水里鄉', N'553', 1),
    (160, 1, N'南投縣', N'魚池鄉', N'555', 1),
    (161, 1, N'南投縣', N'信義鄉', N'556', 1),
    (162, 1, N'南投縣', N'竹山鎮', N'557', 1),
    (163, 1, N'南投縣', N'鹿谷鄉', N'558', 1),
    -- 雲林縣
    (164, 1, N'雲林縣', N'斗南鎮', N'630', 1),
    (165, 1, N'雲林縣', N'大埤鄉', N'631', 1),
    (166, 1, N'雲林縣', N'虎尾鎮', N'632', 1),
    (167, 1, N'雲林縣', N'土庫鎮', N'633', 1),
    (168, 1, N'雲林縣', N'褒忠鄉', N'634', 1),
    (169, 1, N'雲林縣', N'東勢鄉', N'635', 1),
    (170, 1, N'雲林縣', N'台西鄉', N'636', 1),
    (171, 1, N'雲林縣', N'崙背鄉', N'637', 1),
    (172, 1, N'雲林縣', N'麥寮鄉', N'638', 1),
    (173, 1, N'雲林縣', N'斗六市', N'640', 1),
    (174, 1, N'雲林縣', N'林內鄉', N'643', 1),
    (175, 1, N'雲林縣', N'古坑鄉', N'646', 1),
    (176, 1, N'雲林縣', N'莿桐鄉', N'647', 1),
    (177, 1, N'雲林縣', N'西螺鎮', N'648', 1),
    (178, 1, N'雲林縣', N'二崙鄉', N'649', 1),
    (179, 1, N'雲林縣', N'北港鎮', N'651', 1),
    (180, 1, N'雲林縣', N'水林鄉', N'652', 1),
    (181, 1, N'雲林縣', N'口湖鄉', N'653', 1),
    (182, 1, N'雲林縣', N'四湖鄉', N'654', 1),
    (183, 1, N'雲林縣', N'元長鄉', N'655', 1),
    -- 嘉義
    (184, 1, N'嘉義市', N'東區', N'600', 1),
    (185, 1, N'嘉義市', N'西區', N'600', 1),
    (186, 1, N'嘉義縣', N'番路鄉', N'602', 1),
    (187, 1, N'嘉義縣', N'梅山鄉', N'603', 1),
    (188, 1, N'嘉義縣', N'竹崎鄉', N'604', 1),
    (189, 1, N'嘉義縣', N'阿里山鄉', N'605', 1),
    (190, 1, N'嘉義縣', N'中埔鄉', N'606', 1),
    (191, 1, N'嘉義縣', N'大埔鄉', N'607', 1),
    (192, 1, N'嘉義縣', N'水上鄉', N'608', 1),
    (193, 1, N'嘉義縣', N'鹿草鄉', N'611', 1),
    (194, 1, N'嘉義縣', N'太保市', N'612', 1),
    (195, 1, N'嘉義縣', N'朴子市', N'613', 1),
    (196, 1, N'嘉義縣', N'東石鄉', N'614', 1),
    (197, 1, N'嘉義縣', N'六腳鄉', N'615', 1),
    (198, 1, N'嘉義縣', N'新港鄉', N'616', 1),
    (199, 1, N'嘉義縣', N'民雄鄉', N'621', 1),
    (200, 1, N'嘉義縣', N'大林鎮', N'622', 1),
    (201, 1, N'嘉義縣', N'溪口鄉', N'623', 1),
    (202, 1, N'嘉義縣', N'義竹鄉', N'624', 1),
    (203, 1, N'嘉義縣', N'布袋鎮', N'625', 1),
    -- 台南市
    (204, 1, N'台南市', N'中西區', N'700', 1),
    (205, 1, N'台南市', N'東區', N'701', 1),
    (206, 1, N'台南市', N'南區', N'702', 1),
    (207, 1, N'台南市', N'北區', N'704', 1),
    (208, 1, N'台南市', N'安平區', N'708', 1),
    (209, 1, N'台南市', N'安南區', N'709', 1),
    (210, 1, N'台南市', N'永康區', N'710', 1),
    (211, 1, N'台南市', N'歸仁區', N'711', 1),
    (212, 1, N'台南市', N'新化區', N'712', 1),
    (213, 1, N'台南市', N'左鎮區', N'713', 1),
    (214, 1, N'台南市', N'玉井區', N'714', 1),
    (215, 1, N'台南市', N'楠西區', N'715', 1),
    (216, 1, N'台南市', N'南化區', N'716', 1),
    (217, 1, N'台南市', N'仁德區', N'717', 1),
    (218, 1, N'台南市', N'關廟區', N'718', 1),
    (219, 1, N'台南市', N'龍崎區', N'719', 1),
    (220, 1, N'台南市', N'官田區', N'720', 1),
    (221, 1, N'台南市', N'麻豆區', N'721', 1),
    (222, 1, N'台南市', N'佳里區', N'722', 1),
    (223, 1, N'台南市', N'西港區', N'723', 1),
    (224, 1, N'台南市', N'七股區', N'724', 1),
    (225, 1, N'台南市', N'將軍區', N'725', 1),
    (226, 1, N'台南市', N'學甲區', N'726', 1),
    (227, 1, N'台南市', N'北門區', N'727', 1),
    (228, 1, N'台南市', N'新營區', N'730', 1),
    (229, 1, N'台南市', N'後壁區', N'731', 1),
    (230, 1, N'台南市', N'白河區', N'732', 1),
    (231, 1, N'台南市', N'東山區', N'733', 1),
    (232, 1, N'台南市', N'六甲區', N'734', 1),
    (233, 1, N'台南市', N'下營區', N'735', 1),
    (234, 1, N'台南市', N'柳營區', N'736', 1),
    (235, 1, N'台南市', N'鹽水區', N'737', 1),
    (236, 1, N'台南市', N'善化區', N'741', 1),
    (237, 1, N'台南市', N'大內區', N'742', 1),
    (238, 1, N'台南市', N'山上區', N'743', 1),
    (239, 1, N'台南市', N'新市區', N'744', 1),
    (240, 1, N'台南市', N'安定區', N'745', 1),
    -- 高雄市
    (241, 1, N'高雄市', N'新興區', N'800', 1),
    (242, 1, N'高雄市', N'前金區', N'801', 1),
    (243, 1, N'高雄市', N'苓雅區', N'802', 1),
    (244, 1, N'高雄市', N'鹽埕區', N'803', 1),
    (245, 1, N'高雄市', N'鼓山區', N'804', 1),
    (246, 1, N'高雄市', N'旗津區', N'805', 1),
    (247, 1, N'高雄市', N'前鎮區', N'806', 1),
    (248, 1, N'高雄市', N'三民區', N'807', 1),
    (249, 1, N'高雄市', N'楠梓區', N'811', 1),
    (250, 1, N'高雄市', N'小港區', N'812', 1),
    (251, 1, N'高雄市', N'左營區', N'813', 1),
    (252, 1, N'高雄市', N'仁武區', N'814', 1),
    (253, 1, N'高雄市', N'大社區', N'815', 1),
    (254, 1, N'高雄市', N'岡山區', N'820', 1),
    (255, 1, N'高雄市', N'路竹區', N'821', 1),
    (256, 1, N'高雄市', N'阿蓮區', N'822', 1),
    (257, 1, N'高雄市', N'田寮區', N'823', 1),
    (258, 1, N'高雄市', N'燕巢區', N'824', 1),
    (259, 1, N'高雄市', N'橋頭區', N'825', 1),
    (260, 1, N'高雄市', N'梓官區', N'826', 1),
    (261, 1, N'高雄市', N'彌陀區', N'827', 1),
    (262, 1, N'高雄市', N'永安區', N'828', 1),
    (263, 1, N'高雄市', N'湖內區', N'829', 1),
    (264, 1, N'高雄市', N'鳳山區', N'830', 1),
    (265, 1, N'高雄市', N'大寮區', N'831', 1),
    (266, 1, N'高雄市', N'林園區', N'832', 1),
    (267, 1, N'高雄市', N'鳥松區', N'833', 1),
    (268, 1, N'高雄市', N'大樹區', N'840', 1),
    (269, 1, N'高雄市', N'旗山區', N'842', 1),
    (270, 1, N'高雄市', N'美濃區', N'843', 1),
    (271, 1, N'高雄市', N'六龜區', N'844', 1),
    (272, 1, N'高雄市', N'內門區', N'845', 1),
    (273, 1, N'高雄市', N'杉林區', N'846', 1),
    (274, 1, N'高雄市', N'甲仙區', N'847', 1),
    (275, 1, N'高雄市', N'桃源區', N'848', 1),
    (276, 1, N'高雄市', N'那瑪夏區', N'849', 1),
    (277, 1, N'高雄市', N'茂林區', N'851', 1),
    (278, 1, N'高雄市', N'茄萣區', N'852', 1),
    -- 屏東縣
    (279, 1, N'屏東縣', N'屏東市', N'900', 1),
    (280, 1, N'屏東縣', N'三地門鄉', N'901', 1),
    (281, 1, N'屏東縣', N'霧台鄉', N'902', 1),
    (282, 1, N'屏東縣', N'瑪家鄉', N'903', 1),
    (283, 1, N'屏東縣', N'九如鄉', N'904', 1),
    (284, 1, N'屏東縣', N'里港鄉', N'905', 1),
    (285, 1, N'屏東縣', N'高樹鄉', N'906', 1),
    (286, 1, N'屏東縣', N'鹽埔鄉', N'907', 1),
    (287, 1, N'屏東縣', N'長治鄉', N'908', 1),
    (288, 1, N'屏東縣', N'麟洛鄉', N'909', 1),
    (289, 1, N'屏東縣', N'竹田鄉', N'911', 1),
    (290, 1, N'屏東縣', N'內埔鄉', N'912', 1),
    (291, 1, N'屏東縣', N'萬丹鄉', N'913', 1),
    (292, 1, N'屏東縣', N'潮州鎮', N'920', 1),
    (293, 1, N'屏東縣', N'泰武鄉', N'921', 1),
    (294, 1, N'屏東縣', N'來義鄉', N'922', 1),
    (295, 1, N'屏東縣', N'萬巒鄉', N'923', 1),
    (296, 1, N'屏東縣', N'崁頂鄉', N'924', 1),
    (297, 1, N'屏東縣', N'新埤鄉', N'925', 1),
    (298, 1, N'屏東縣', N'南州鄉', N'926', 1),
    (299, 1, N'屏東縣', N'林邊鄉', N'927', 1),
    (300, 1, N'屏東縣', N'東港鎮', N'928', 1),
    (301, 1, N'屏東縣', N'琉球鄉', N'929', 1),
    (302, 1, N'屏東縣', N'佳冬鄉', N'931', 1),
    (303, 1, N'屏東縣', N'新園鄉', N'932', 1),
    (304, 1, N'屏東縣', N'枋寮鄉', N'940', 1),
    (305, 1, N'屏東縣', N'枋山鄉', N'941', 1),
    (306, 1, N'屏東縣', N'春日鄉', N'942', 1),
    (307, 1, N'屏東縣', N'獅子鄉', N'943', 1),
    (308, 1, N'屏東縣', N'車城鄉', N'944', 1),
    (309, 1, N'屏東縣', N'牡丹鄉', N'945', 1),
    (310, 1, N'屏東縣', N'恆春鎮', N'946', 1),
    (311, 1, N'屏東縣', N'滿州鄉', N'947', 1),
    -- 宜蘭縣
    (312, 1, N'宜蘭縣', N'宜蘭市', N'260', 1),
    (313, 1, N'宜蘭縣', N'頭城鎮', N'261', 1),
    (314, 1, N'宜蘭縣', N'礁溪鄉', N'262', 1),
    (315, 1, N'宜蘭縣', N'壯圍鄉', N'263', 1),
    (316, 1, N'宜蘭縣', N'員山鄉', N'264', 1),
    (317, 1, N'宜蘭縣', N'羅東鎮', N'265', 1),
    (318, 1, N'宜蘭縣', N'三星鄉', N'266', 1),
    (319, 1, N'宜蘭縣', N'大同鄉', N'267', 1),
    (320, 1, N'宜蘭縣', N'五結鄉', N'268', 1),
    (321, 1, N'宜蘭縣', N'冬山鄉', N'269', 1),
    (322, 1, N'宜蘭縣', N'蘇澳鎮', N'270', 1),
    (323, 1, N'宜蘭縣', N'南澳鄉', N'272', 1),
    -- 花蓮縣
    (324, 1, N'花蓮縣', N'花蓮市', N'970', 1),
    (325, 1, N'花蓮縣', N'新城鄉', N'971', 1),
    (326, 1, N'花蓮縣', N'秀林鄉', N'972', 1),
    (327, 1, N'花蓮縣', N'吉安鄉', N'973', 1),
    (328, 1, N'花蓮縣', N'壽豐鄉', N'974', 1),
    (329, 1, N'花蓮縣', N'鳳林鎮', N'975', 1),
    (330, 1, N'花蓮縣', N'光復鄉', N'976', 1),
    (331, 1, N'花蓮縣', N'豐濱鄉', N'977', 1),
    (332, 1, N'花蓮縣', N'瑞穗鄉', N'978', 1),
    (333, 1, N'花蓮縣', N'萬榮鄉', N'979', 1),
    (334, 1, N'花蓮縣', N'玉里鎮', N'981', 1),
    (335, 1, N'花蓮縣', N'卓溪鄉', N'982', 1),
    (336, 1, N'花蓮縣', N'富里鄉', N'983', 1),
    -- 台東縣
    (337, 1, N'台東縣', N'台東市', N'950', 1),
    (338, 1, N'台東縣', N'綠島鄉', N'951', 1),
    (339, 1, N'台東縣', N'蘭嶼鄉', N'952', 1),
    (340, 1, N'台東縣', N'延平鄉', N'953', 1),
    (341, 1, N'台東縣', N'卑南鄉', N'954', 1),
    (342, 1, N'台東縣', N'鹿野鄉', N'955', 1),
    (343, 1, N'台東縣', N'關山鎮', N'956', 1),
    (344, 1, N'台東縣', N'海端鄉', N'957', 1),
    (345, 1, N'台東縣', N'池上鄉', N'958', 1),
    (346, 1, N'台東縣', N'東河鄉', N'959', 1),
    (347, 1, N'台東縣', N'成功鎮', N'961', 1),
    (348, 1, N'台東縣', N'長濱鄉', N'962', 1),
    (349, 1, N'台東縣', N'太麻里鄉', N'963', 1),
    (350, 1, N'台東縣', N'金峰鄉', N'964', 1),
    (351, 1, N'台東縣', N'大武鄉', N'965', 1),
    (352, 1, N'台東縣', N'達仁鄉', N'966', 1),
    -- 澎湖縣
    (353, 1, N'澎湖縣', N'馬公市', N'880', 1),
    (354, 1, N'澎湖縣', N'西嶼鄉', N'881', 1),
    (355, 1, N'澎湖縣', N'望安鄉', N'882', 1),
    (356, 1, N'澎湖縣', N'七美鄉', N'883', 1),
    (357, 1, N'澎湖縣', N'白沙鄉', N'884', 1),
    (358, 1, N'澎湖縣', N'湖西鄉', N'885', 1),
    -- 連江縣
    (359, 1, N'連江縣', N'南竿鄉', N'209', 1),
    (360, 1, N'連江縣', N'北竿鄉', N'210', 1),
    (361, 1, N'連江縣', N'莒光鄉', N'211', 1),
    (362, 1, N'連江縣', N'東引鄉', N'212', 1),
    -- 金門縣
    (363, 1, N'金門縣', N'金沙鎮', N'890', 1),
    (364, 1, N'金門縣', N'金湖鎮', N'891', 1),
    (365, 1, N'金門縣', N'金寧鄉', N'892', 1),
    (366, 1, N'金門縣', N'金城鎮', N'893', 1),
    (367, 1, N'金門縣', N'烈嶼鄉', N'894', 1),
    (368, 1, N'金門縣', N'烏坵鄉', N'896', 1);
END
GO

PRINT N'Ceremony database initialization completed successfully.';
GO
