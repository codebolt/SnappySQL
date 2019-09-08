CREATE TABLE [dbo].[Teacher]
(
	[Id] INT NOT NULL IDENTITY PRIMARY KEY,
	[Name] VARCHAR(128),
	[Address] VARCHAR(512),
	[Telephone] VARCHAR(16),
	[Email] VARCHAR(128)
)
