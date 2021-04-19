# Volume Entry Data Structures

## Volume Head Entry

| Variable | Expected Values | Size (Bytes) |
| --- | --- | --- |
| Known file signature: | 0xFA4B | 2 |
| Major API version: | 0 | 1 |
| Minor API version: | 1 | 1 |
| Volume size: | (user input) | 8 |
| Logical page size: | (user input) | 8 |
| Padding: | null | 10 |
| Known entry end signature: | 0xFFFF | 2 |
| | **Total:** | 32 |
<br>

## FAT Page

512-byte page of 4-byte entries that correspond to the status of a logical data cluster:

| Entry Type | Entry Value |
| --- | --- |
| Next cluster of file: | (4-byte address) |
| End of file: | 0xFFFF FFFF |
| Unallocated cluster: | 0x0000 0000 |
| End of FAT page: | 0xFFFF FA4B (next 4 bytes are address of next FAT page or 0) |
<br>

## Directory Entry Page

1024-byte page of 96-byte entries that correspond to a single file. If a directory has more entries than a page can hold, another one is added for that directory and the new page is linked to the previous entry page. Creating a directory also prompts the creation of a directory entry page for its contents.

End of directory entry page: 0xFFFF FFFF FFFF FFFF (next 4 bytes are address of next page if one exists or 0x0000 0000 if this is the last directory entry page of that directory).
<br><br>

## Directory Entries

| Variable | Data Type | Size (Bytes) |
| --- | --- | --- |
| File name: | String | 39 |
| File attributes: | Byte | 1 |
| Padding: | Null | 24 |
| Creation datetime: | 64-bit CLR datetime object | 8 |
| Modified datetime: | 64-bit CLR datetime object | 8 |
| Start cluster: | 64-bit integer | 8 |
| File size: | 64-bit integer | 8 |
| | **Total:** | 96 |
<br>

## Attribute Byte

| Bit | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Attribute (bool) | Read-only | Hidden | System | Label | Directory | Archive | N/A | N/A |
