// Runs the settler draw path of Siedler1.exe in Ghidra's emulator and prints what it draws. It extends
// EmulateMapStart.java (the harness of ADR 0008), whose verbs set up the game, and adds the verb draw. Run it through
// emulate.py (see README.md), which picks this script for draw: and passes the install folder in EMU_GAME.
//
//   draw:WINDOWS:CX,CY:VERB:ARGS...
//                            VERB:ARGS is a command of carrier, wood, or goods (EmulateMapStart.java): the same game and the
//                            same passes. After each pass from the first of WINDOWS (FROM-TO,FROM-TO,..., pass numbers from
//                            0, both ends included) the verb runs the original's settler draw path as the main loop 0x425a10
//                            does after the simulation: the view pass 0x42f4f1 (each settler on a shown tile takes one
//                            more turn, 0x42fc81, and its draw record is built by 0x42f512 and 0x43f537 through the job's
//                            figure handler), then for each row of the view 0x44ffe7 draws the row's records through
//                            0x406610. The passes before the first window run without the view, as the verbs above do; the
//                            view then looks on to the end, so a settler in it takes a turn every pass, and the windows
//                            pick the passes the recording keeps.
//                            The view is the single 640 by 480 view of 0x4200b6 (38 columns, 28 rows, offsets 0x10 and
//                            0x0e) looking at the node CX,CY; its screen origin is set so a drawn position is the node's map
//                            pixel (32 * x - 16 * y, 20 * y) plus the 0x10 and 8 that 0x44ffe7 adds. A line per pass in a
//                            window lists the clock, the 16 bytes of each settler whose bytes changed since the line before
//                            and the height byte of its node (s<slot>=bytes/height), and what the pass drew (d=): for each
//                            settler record, in the order of the rows and of the records in a row, the settler's slot and the
//                            sprites 0x406610 blits, as slot:entry/colour/x,y;... with the colour -1 for a sprite blitted as
//                            it is (0x405590) and the palette index for a colour overlay (0x405880); records are separated
//                            by |. The shadow before each settler (0x4061a0) and the sound request (0x44fe13) are skipped.
//                            The run fails, and prints no line, where a pass of a window draws no settler or no window pass
//                            is reached.
//
// The graphics archive is read from the install (loca/SPAD.PA) into the emulator and the rule data of its entry 2 is
// prepared as RuleData_Load (0x41e8e0) does, because that routine reaches the archive through the register block at its
// program address, which the harness does not use.
//@category OpenSettlers

import java.io.File;
import java.nio.file.Files;
import java.util.ArrayList;
import java.util.List;

public class EmulateDraw extends EmulateMapStart {

    private static final long GAME_VIEW = PLAYERS + 0x9000L;
    private static final long OTHER_VIEW = PLAYERS + 0x9400L;
    private static final long ROW_LISTS = 0x26000000L;
    private static final long ARCHIVE = 0x30000000L;
    private static final long ARCHIVE_BASE = 0x6812ecL;
    private static final long PROGRAM_REGISTERS = 0x674000L;

    private static final long VIEW_PASS = 0x42f4f1L;
    private static final long ROW_DRAW = 0x44ffe7L;
    private static final long DRAW_SETTLER = 0x406610L;
    private static final long BLIT = 0x405590L;
    private static final long BLIT_COLOUR = 0x405880L;
    private static final long SHADOW = 0x4061a0L;
    private static final long SOUND = 0x44fe13L;
    // The record builder 0x43f537, which 0x42f512 calls at 0x42f7a0: its settler is the item at +0x24 of the register block.
    private static final long RECORD_BUILDER = 0x43f537L;
    private static final long RECORD_BUILT = 0x42f7a5L;
    private static final long[] STOPS = {
        RETURN, DRAW_SETTLER, BLIT, BLIT_COLOUR, SHADOW, SOUND, RECORD_BUILDER, RECORD_BUILT
    };

    // The 640 by 480 view (0x4200b6): 38 columns, 28 rows, the scan starts 0x10 columns and 0x0e rows before the node.
    private static final int VIEW_COLUMNS = 0x26;
    private static final int VIEW_ROWS = 0x1c;
    private static final int ROW_LIST_BYTES = 0x1fa;
    private static final int SETTLER_BYTES = 16 * 64;

    private final List<String> lines = new ArrayList<>();
    private final List<String> noise = new ArrayList<>();
    private int[][] windows;
    private int cameraX;
    private int cameraY;
    private byte[] archive;
    private boolean viewReady;
    private int recorded;
    private byte[] settlersSeen = new byte[SETTLER_BYTES];

    // The settler of each record a row's list holds, in the order of the list (found when 0x43f537 appends a record: the
    // row whose list pointer moved), and the records drawn: the settler's slot and the sprites of the draw being run.
    private final List<List<Integer>> rowSettlers = new ArrayList<>();
    private int builderSlot;
    private byte[] listPointers;
    private int row;
    private int rowIndex;
    private int slot;
    private final List<String> drawn = new ArrayList<>();
    private final List<String> sprites = new ArrayList<>();
    private int blits;
    private int first;
    private int second;

    @Override
    public void println(String message) {
        if (message.startsWith("EMU draw ")) {
            super.println(message);
        } else {
            noise.add(message);
        }
    }

    @Override
    protected void draw(String rest) throws Exception {
        String[] parts = rest.split(":", 3);
        windows = windows(parts[0]);
        String[] camera = parts[1].split(",");
        cameraX = Integer.parseInt(camera[0]);
        cameraY = Integer.parseInt(camera[1]);
        String verb = parts[2].substring(0, parts[2].indexOf(':'));
        String args = parts[2].substring(parts[2].indexOf(':') + 1);
        archive = Files.readAllBytes(archiveFile().toPath());
        switch (verb) {
            case "carrier" -> carrier(args);
            case "wood" -> worker(args, true);
            case "goods" -> {
                reserveFlagZero = true;
                defaultPriorities = true;
                worker(args, true);
            }
            default -> throw new IllegalArgumentException("draw: runs carrier, wood, or goods, not " + verb);
        }
        for (String line : noise) {
            if (line.contains("REFUSED")) {
                throw new IllegalStateException("the game did not start as asked: " + line);
            }
        }
        if (recorded == 0) {
            throw new IllegalStateException("no pass of the windows was reached: the run never reached the draw path");
        }
        super.println("EMU draw view cam=" + cameraX + "," + cameraY + " passes=" + recorded);
        for (String line : lines) {
            super.println(line);
        }
        super.println("EMU draw end passes=" + recorded);
    }

    private static int[][] windows(String list) {
        String[] parts = list.split(",");
        int[][] result = new int[parts.length][];
        for (int i = 0; i < parts.length; i++) {
            String[] ends = parts[i].split("-");
            result[i] = new int[] {Integer.parseInt(ends[0]), Integer.parseInt(ends[1])};
        }
        return result;
    }

    private File archiveFile() {
        String game = System.getenv("EMU_GAME");
        if (game == null) {
            throw new IllegalStateException("EMU_GAME names no install: run through emulate.py");
        }
        File loca = child(new File(game), "loca");
        return child(loca, "spad.pa");
    }

    private static File child(File folder, String name) {
        File[] files = folder.listFiles();
        if (files != null) {
            for (File file : files) {
                if (file.getName().equalsIgnoreCase(name)) {
                    return file;
                }
            }
        }
        throw new IllegalStateException("no " + name + " in " + folder);
    }

    @Override
    protected void afterPass(int pass, int clock) throws Exception {
        boolean inside = false;
        int start = Integer.MAX_VALUE;
        for (int[] window : windows) {
            inside |= pass >= window[0] && pass <= window[1];
            start = Math.min(start, window[0]);
        }
        if (pass < start) {
            return;
        }
        if (!viewReady) {
            prepareView();
            viewReady = true;
        }
        drawn.clear();
        rowSettlers.clear();
        for (int i = 0; i <= VIEW_ROWS; i++) {
            rowSettlers.add(new ArrayList<>());
        }
        runWithHooks(VIEW_PASS);
        putInt(REGISTERS + 0x20, GAME_VIEW);
        putInt(REGISTERS + 0x2c, getInt(GAME_VIEW + 0x92));
        for (row = 0; row <= VIEW_ROWS; row++) {
            rowIndex = 0;
            runWithHooks(ROW_DRAW);
            endRecord();
        }
        if (!inside) {
            return;
        }
        if (drawn.isEmpty()) {
            throw new IllegalStateException("pass " + pass + " draws no settler");
        }
        StringBuilder line = new StringBuilder("EMU draw pass=" + pass + " clock=" + clock);
        byte[] now = emu.readMemory(toAddr(TABLES + 0x40000), SETTLER_BYTES);
        for (int slot = 0; slot < 64; slot++) {
            boolean changed = false;
            for (int i = 0; i < 16; i++) {
                changed |= now[16 * slot + i] != settlersSeen[16 * slot + i];
            }
            if (changed) {
                // The 16 bytes, then the height byte of the node the settler stands on (the map's byte 1, 0x43f537).
                long node = getInt(TABLES + 0x40000 + 16L * slot + 4);
                line.append(" s").append(slot).append("=").append(hex(TABLES + 0x40000 + 16L * slot, 16)).append("/")
                        .append(hex(getInt(STATE + 0x24) + node + 1, 1));
            }
        }
        settlersSeen = now.clone();
        line.append(" d=").append(String.join("|", drawn));
        lines.add(line.toString());
        recorded++;
    }

    /** Builds the view and loads the archive: the state the draw path reads and the main loop has built by then. */
    private void prepareView() throws Exception {
        byte[] bytes = archive.clone();
        int offset = le(bytes, 8 * 2 + 4);
        // RuleData_Load: skip 4 bytes, then 200 dwords stored big-endian become little-endian in place.
        for (int i = 0; i < 200; i++) {
            int at = offset + 4 + 4 * i;
            byte a = bytes[at];
            byte b = bytes[at + 1];
            bytes[at] = bytes[at + 3];
            bytes[at + 1] = bytes[at + 2];
            bytes[at + 2] = b;
            bytes[at + 3] = a;
        }
        emu.writeMemory(toAddr(ARCHIVE), bytes);
        putInt(ARCHIVE_BASE, ARCHIVE);
        putInt(STATE + 0xb0, ARCHIVE + offset + 4);
        zero(ROW_LISTS, 0x74 + (VIEW_ROWS + 1) * ROW_LIST_BYTES + 0x100);
        zero(GAME_VIEW, 0x400);
        zero(GAME_VIEW + 0x400, 0x400);
        putWord(GAME_VIEW + 0x20, VIEW_COLUMNS);
        putWord(GAME_VIEW + 0x50, VIEW_ROWS);
        putWord(GAME_VIEW + 0x4a, 0x10);
        putWord(GAME_VIEW + 0x4c, 0x0e);
        putWord(GAME_VIEW + 0x46, cameraX);
        putWord(GAME_VIEW + 0x48, cameraY);
        putWord(GAME_VIEW + 0x9a, 32 * cameraX - 16 * cameraY);
        putWord(GAME_VIEW + 0x90, 20 * cameraY);
        putInt(GAME_VIEW + 0x92, ROW_LISTS);
        putInt(GAME_VIEW + 0x1a, GAME_VIEW + 0x200);
        emu.writeMemory(toAddr(OTHER_VIEW), new byte[] {1});
    }

    private static int le(byte[] bytes, int at) {
        return (bytes[at] & 0xff) | ((bytes[at + 1] & 0xff) << 8) | ((bytes[at + 2] & 0xff) << 16)
                | ((bytes[at + 3] & 0xff) << 24);
    }

    /** Runs the routine at entry to its return with the draw hooks set. */
    private void runWithHooks(long entry) throws Exception {
        long esp = emu.readRegister("ESP").longValue() - 4;
        emu.writeRegister("ESP", esp);
        putInt(esp, RETURN);
        emu.writeRegister(emu.getPCRegister(), entry);
        for (long stop : STOPS) {
            emu.setBreakpoint(toAddr(stop));
        }
        try {
            while (true) {
                emu.run(monitor);
                if (emu.getLastError() != null) {
                    throw new IllegalStateException("emulation failed: " + emu.getLastError());
                }
                long pc = emu.readRegister(emu.getPCRegister()).longValue();
                if (pc == RETURN) {
                    break;
                } else if (pc == DRAW_SETTLER) {
                    beginDraw();
                    emu.step(monitor);
                } else if (pc == BLIT || pc == BLIT_COLOUR) {
                    blit(pc == BLIT_COLOUR);
                    ret();
                } else if (pc == SHADOW || pc == SOUND) {
                    ret();
                } else if (pc == RECORD_BUILDER) {
                    builderSlot = (int) ((getInt(REGISTERS + 0x24) - (TABLES + 0x40000)) / 16);
                    listPointers = emu.readMemory(toAddr(ROW_LISTS), 4 * (VIEW_ROWS + 1));
                    emu.step(monitor);
                } else if (pc == RECORD_BUILT) {
                    byte[] after = emu.readMemory(toAddr(ROW_LISTS), 4 * (VIEW_ROWS + 1));
                    for (int i = 0; i <= VIEW_ROWS; i++) {
                        for (int k = 0; k < 4; k++) {
                            if (after[4 * i + k] != listPointers[4 * i + k]) {
                                rowSettlers.get(i).add(builderSlot);
                                break;
                            }
                        }
                    }
                    emu.step(monitor);
                } else {
                    throw new IllegalStateException("stopped at " + Long.toHexString(pc));
                }
            }
        } finally {
            for (long stop : STOPS) {
                emu.clearBreakpoint(toAddr(stop));
            }
        }
        emu.writeRegister("ESP", esp + 4);
    }

    /** Returns from the routine the program counter stands at the entry of, as its ret does. */
    private void ret() throws Exception {
        long esp = emu.readRegister("ESP").longValue();
        emu.writeRegister(emu.getPCRegister(), getInt(esp));
        emu.writeRegister("ESP", esp + 4);
    }

    /**
     * The entry of 0x406610: the routine reads the register block at its program address, as the program's EDI points
     * there, so the block is copied there; the figure and second figure words are read for the entry numbers.
     */
    private void beginDraw() throws Exception {
        endRecord();
        emu.writeMemory(toAddr(PROGRAM_REGISTERS), emu.readMemory(toAddr(REGISTERS), 0x40));
        first = (short) getWord(REGISTERS + 0x10);
        second = (short) getWord(REGISTERS + 0xc);
        blits = 0;
        List<Integer> settlers = rowSettlers.get(row);
        if (rowIndex >= settlers.size()) {
            throw new IllegalStateException("row " + row + " draws a record no settler built");
        }
        slot = settlers.get(rowIndex++);
    }

    /** Closes the record being drawn: the settler's slot and its sprites. */
    private void endRecord() {
        if (!sprites.isEmpty()) {
            drawn.add(slot + ":" + String.join(";", sprites));
            sprites.clear();
        }
    }

    /** A blit call of 0x406610: the sprite pointer in EDX, then x and y (and the colour) on the stack. */
    private void blit(boolean colour) throws Exception {
        long esp = emu.readRegister("ESP").longValue();
        long edx = emu.readRegister("EDX").longValue() & 0xffffffffL;
        int x = (short) getInt(esp + 4);
        int y = (short) getInt(esp + 8);
        int entry = blits == 0 ? 1850 + first : blits == 1 ? 2500 + first : 3150 + second;
        int offset = le(archive, 8 * entry + 4);
        long expected = offset == 0 ? 0 : ARCHIVE + offset;
        if (edx != expected || colour != (blits == 1) || blits > 2) {
            throw new IllegalStateException("blit " + blits + " of the draw is not the entry " + entry);
        }
        sprites.add(entry + "/" + (colour ? (int) (getInt(esp + 12) & 0xffff) : -1) + "/" + x + "," + y);
        blits++;
    }
}
