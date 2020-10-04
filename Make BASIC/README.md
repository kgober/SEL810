This utility can be used to create a SEL810 BASIC program paper tape image.

Example:
```
C:\>MakeBASIC.exe hello.bin
line 10
print
str HELLO, WORLD!
endstr
line 20
goto 10
line 30
end
make
Program lines: 3
Writing 127 leader bytes...
Writing header...
Writing 18 words of program text...
Checksum: ee66
Writing 128 trailer bytes...

C:\>TapeDump.exe -q hello.bin
10  PRINT "HELLO, WORLD!"
20  GOTO 10
30  END

C:\>
```
