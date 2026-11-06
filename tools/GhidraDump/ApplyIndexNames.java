// Applies the names of the program index (docs/rules/program-index.md, section "By address") to the
// program: a function name at a code address, creating the function when there is none, and a label at
// a data address. A row whose role starts with "Data:" gets a label even in the code section; a
// function at its address, such as one an earlier run created there, is removed with its
// instructions. Run headless (see README.md); the first script argument is the path of the index.
//@category OpenSettlers

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.listing.Function;
import ghidra.program.model.mem.MemoryBlock;
import ghidra.program.model.symbol.SourceType;

public class ApplyIndexNames extends GhidraScript {

    private static final Pattern ROW = Pattern.compile("^\\| `(0x[0-9a-f]+)` \\| `(\\w+)` \\|");
    private static final Pattern DATA_ROW = Pattern.compile("^\\| `(0x[0-9a-f]+)` \\| `\\w+` \\| Data:", Pattern.MULTILINE);

    @Override
    protected void run() throws Exception {
        String text = Files.readString(Path.of(getScriptArgs()[0]));
        int start = text.indexOf("\n## By address");
        int end = text.indexOf("\n## ", start + 1);
        if (start < 0) {
            throw new IllegalArgumentException("no section \"By address\" in the index");
        }
        java.util.TreeSet<Long> rows = new java.util.TreeSet<>();
        Matcher rm = Pattern.compile("^\\| `(0x[0-9a-f]+)` \\|", Pattern.MULTILINE).matcher(text);
        while (rm.find()) {
            rows.add(Long.decode(rm.group(1)));
        }
        java.util.Set<String> data = new java.util.HashSet<>();
        Matcher dm = DATA_ROW.matcher(text);
        while (dm.find()) {
            data.add(dm.group(1));
        }
        int functions = 0;
        int labels = 0;
        int failed = 0;
        for (String line : text.substring(start, end < 0 ? text.length() : end).split("\n")) {
            Matcher m = ROW.matcher(line);
            if (!m.find()) {
                continue;
            }
            Address address = toAddr(m.group(1));
            String name = m.group(2);
            MemoryBlock block = currentProgram.getMemory().getBlock(address);
            try {
                if (block != null && block.isExecute() && !data.contains(m.group(1))) {
                    Function function = getFunctionAt(address);
                    if (function == null) {
                        if (getInstructionAt(address) == null) {
                            disassemble(address);
                        }
                        function = createFunction(address, name);
                    }
                    if (function == null) {
                        printerr("ApplyIndexNames: no function at " + address + " for " + name);
                        failed++;
                        continue;
                    }
                    function.setName(name, SourceType.USER_DEFINED);
                    functions++;
                } else {
                    Function wrong = getFunctionAt(address);
                    if (wrong != null) {
                        // Clear the instructions only up to the next function or index row, so code
                        // that follows the table keeps its listing.
                        long limit = wrong.getBody().getMaxAddress().getOffset() + 1;
                        Long nextRow = rows.higher(address.getOffset());
                        if (nextRow != null) {
                            limit = Math.min(limit, nextRow);
                        }
                        Function next = getFunctionAfter(address);
                        if (next != null && next != wrong) {
                            limit = Math.min(limit, next.getEntryPoint().getOffset());
                        }
                        removeFunction(wrong);
                        clearListing(address, toAddr(limit - 1));
                    }
                    createLabel(address, name, true, SourceType.USER_DEFINED);
                    labels++;
                }
            } catch (Exception e) {
                printerr("ApplyIndexNames: " + address + " " + name + ": " + e.getMessage());
                failed++;
            }
        }
        println("ApplyIndexNames: functions " + functions + ", labels " + labels + ", failed " + failed);
    }
}
