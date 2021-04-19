# Testing

FakeFS interface commands are testing using an automated testing suite run by invoking `FakeFS.exe test` from the terminal. The parser is not tested by the suite, instead object methods are called that mimic the FakeFS interface actions. These tests are run in series followed by assertions that determine whether the operation results in known good values. When a test is passed there is output declaring the interface action tested was successful.

Test Data Structures

1. Instantiate a DirectoryEntry with a file name.
2. Serialize DirectoryEntry to byte array.
3. Instantiate DirectoryEntry from the byte array.
4. Assert DirectoryEntries from steps 1 and 3 are equal.

Test Volume Functions

1. Allocate new volume.
2. Mount volume created in step 1.
3. Assert that volume has been properly mounted.

Test File Functions

1. Test `create`.
    1. Create two files, `emptyFile` and `writeFile`.
    2. Assert both files exist.
2. Test `read` and `write`.
    1. Serialize a string to byte array.
    2. Write byte array to `writeFile`.
    3. Read from `writeFile` count of byte array.
    4. Assert bytes read equal serialized string.
3. Test `set`.
    1. Set read-only attribute of `emptyFile` to true.
    2. Attempt to delete `emptyFile`.
    3. Catch exception raised from attempting to delete read-only file.
    4. Assert exception was raised.
4. Test `delete`.
    1. Set read-only attribute of `emptyFile` to false.
    2. Delete `emptyFile`.
    3. Assert `emptyFile` does not exist.
5. Test `truncate`.
    1. Truncate `writeFile`.
    2. Assert `writeFile` size is now 0.

Test Directory Functions

1. Test `mkdir`.
    1. Create directory `directory`.
    2. Assert `directory` exists.
2. Test `mv`.
    1. Move `writeFile` into directory.
    2. Assert `writeFile` is in directory.
3. Test `cd`.
    1. Change active directory to `directory`.
    2. Assert correct file path and contents.
4. Test `rmdir`.
    1. Attempt to delete `directory`.
    2. Catch exception raised from attempting to delete non-empty directory.
    3. Assert exception was raised.
    4. Delete `emptyFile`.
    5. Delete `directory`.
    6. Assert there are no contents in root directory.

Test Volume Cleanup Functions

1. Test `unmount`.
    1. Unmount mounted volume.
    2. Assert volume not mounted.
2. Test `dump`.
    1. Dump testing volume.
    2. Assert byte array not null.
3. Test `truncate`.
    1. Truncate testing volume.
    2. Assert free space of volume is equal to allocated space.
4. Test `deallocate`.
    1. Deallocate testing volume.
    2. Assert volume file does not exist.
5. Print byte array from step 2 with BytePrinter.

The final output of the test suite is the byte dump of the volume. The output should have no allocated FAT clusters or directory entries. Artifacts will remain in the data block from the test string that was written to `writeFile`. Bytes will remain from files even after deletion until they are written over by being allocated to another file. The volume head, initial FAT page, and root directory entry page will all look like the one [here](FileSystemExample.md).
