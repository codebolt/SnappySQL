CREATE TABLE [dbo].[Class]
(
	[Id] CHAR(7),
	[Year] INT,
	[Term] CHAR, -- (S)pring or (F)all
	[Name] VARCHAR(64),
	[TeacherId] INT,
	CONSTRAINT PK_Class PRIMARY KEY NONCLUSTERED (Id, Year, Term),
	CONSTRAINT FK_Class_Teacher FOREIGN KEY (TeacherId) REFERENCES Teacher (Id)
)
