CREATE TABLE [dbo].[Attendance]
(
	[StudentId] INT,
	[ClassId] CHAR(7),
	[Year] INT,
	[Term] CHAR,
	CONSTRAINT FK_Attendance_Student FOREIGN KEY (StudentId) REFERENCES Student (Id),
	CONSTRAINT FK_Attendance_Class FOREIGN KEY (ClassId, Year, Term) REFERENCES Class (Id, Year, Term)
)
