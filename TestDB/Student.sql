CREATE TABLE [dbo].[Student]
(
	[Id] INT NOT NULL IDENTITY PRIMARY KEY,
	[Name] VARCHAR(128),
	[Address] VARCHAR(512),
	[Gender] CHAR, -- M/F
	[Birthday] DATE
)
