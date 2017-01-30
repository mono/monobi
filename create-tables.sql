BEGIN TRANSACTION;

DROP TABLE IF EXISTS [dbo].[FailedTests];
DROP TABLE IF EXISTS [dbo].[Builds];

DROP INDEX IF EXISTS [dbo].[PK_Table];
DROP INDEX IF EXISTS [dbo].[PK_Builds];
DROP INDEX IF EXISTS [dbo].[PK_FailedTests];
DROP INDEX IF EXISTS [dbo].[FK_FailedTests_Builds];

CREATE TABLE [dbo].[Builds]
(
	[JobName] NVARCHAR(196) NOT NULL,
	[PlatformName] NVARCHAR(196) NOT NULL,
	[BuildId] INT NOT NULL,
	[Result] NCHAR(8) NOT NULL,
	[DateTime] DATETIME NOT NULL,
	[URL] NVARCHAR(MAX) NOT NULL,
	CONSTRAINT [PK_Builds] PRIMARY KEY ([JobName], [PlatformName], [BuildId])
);

CREATE TABLE [dbo].[FailedTests]
(
	[JobName] NVARCHAR(196) NOT NULL,
	[PlatformName] NVARCHAR(196) NOT NULL,
	[BuildId] INT NOT NULL,
	[TestName] NVARCHAR(MAX) NOT NULL,
	CONSTRAINT [PK_FailedTests] PRIMARY KEY ([JobName], [PlatformName], [BuildId], [TestName]),
	CONSTRAINT [FK_FailedTests_Builds] FOREIGN KEY ([JobName], [PlatformName], [BuildId])
		REFERENCES [dbo].[Builds] ([JobName], [PlatformName], [BuildId])
		ON DELETE CASCADE
		ON UPDATE CASCADE
);

COMMIT;