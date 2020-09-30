This utility can be used to create a SEL810 BASIC program paper tape image.

Example:
```
C:\>MakeBASIC.exe hello.bin
line 10
str HELLO, WORLD!
endstr
line 20
goto 10
line 30
end
make
Program lines: 3
Writing 125 leader bytes...
Writing program size...
Writing 18 words of program text...
Checksum: ee66
Writing 128 trailer bytes...
C:\>
```
