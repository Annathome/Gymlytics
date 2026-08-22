CREATE DATABASE StudentDB;
USE StudentDB;

CREATE TABLE tblStudent (
    studId INT PRIMARY KEY,
    Student_name VARCHAR(50),
    Allowance DECIMAL(10,2),
    Date DATE
);

CREATE TABLE tblDetails (
    studId INT,
    address VARCHAR(50),
    gender VARCHAR(10),
    FOREIGN KEY (studId) REFERENCES tblStudent(studId)
);

INSERT INTO tblStudent (studId, Student_name, Allowance, Date) VALUES
(1, 'Samantha', 1500, '2010-06-01'),
(2, 'Lynn', 250, '2010-06-07'),
(3, 'Mark', 300, '2010-06-08'),
(4, 'Samantha', 700, '2010-06-08');

INSERT INTO tblDetails (studId, address, gender) VALUES
(1, 'mandauE', 'female'),
(2, 'mabolo', 'female'),
(3, 'bulacao', 'male'),
(4, 'mandauE', 'female');

SELECT 
    s.Student_name,
    s.Allowance,
    d.address,
    d.gender,
    s.Date
FROM tblStudent AS s
INNER JOIN tblDetails AS d
ON s.studId = d.studId;
