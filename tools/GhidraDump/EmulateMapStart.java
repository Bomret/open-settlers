// Emulates the map-start code of Siedler1.exe and prints the values it produces. It is the emulator harness
// of ADR 0008: it reads the program from the Ghidra project it runs in and commits no program bytes. Run it
// through emulate.py (see README.md), which imports Siedler1.exe from the install.
//
// The game code keeps its registers in a block that EDI points to, and the game state at the block's +0x34
// pointer. The script builds both in unused memory, loads the input, runs the original's instructions from
// a start address to a stop address, and reads the result back. Each script argument is one command, with
// hexadecimal words and no spaces:
//
//   rng:S0,S1,S2             one Rng_Step (0x466ade) from the state words S0,S1,S2; prints the new words
//   steps:N:S0,S1,S2         N steps in a row; prints the words after each
//   parse:CCCCCCCCCCCCCCCC   the seed parse (0x467e12 to 0x467f39) of 16 typed characters; prints the three
//                            seed words, or REFUSED where the code takes the error exit (0x467f49)
//   show:W0,W1,W2            the four 12-bit groups and their four characters each (0x467831 to 0x467941)
//   mask:W0,W1,W2            the map seed words the mask code (0x468b3d) stores from the seed words
//   gen:N:S0,S1,S2           MapGen_Generate (0x4216e9) for the size value N (the value Game_SetStartParams hands
//                            on) and the map seed words S0,S1,S2, from the size loop of StartScreen_StartGame
//                            (0x4685cf) through Map_SetGeometry (0x42195c) and the first two stores of
//                            MapGen_InitParams (0x421530), the spiral table builder (0x41e726) and the object
//                            size table pointer, to the end of MapGen_CountResource1 (0x421841). Prints the
//                            geometry, then after the seeding the generator words, and after each pass the
//                            generator words and one FNV-1a 64-bit digest per layer of the canonical map form
//                            (docs/rules/map-form.md); after MapGen_CountResource1 also the total
//   tables:x                 the digest of the first 256 spiral table entries, the seeds of its groups of six,
//                            and the object codes whose size byte is above 1, then the size byte of every code as runs
//   site:N:S0,S1,S2          the build-site evaluator (0x44b6e6, docs/rules/build-sites.md) on every tile of the
//                            map gen:N:S0,S1,S2 makes, for a player without a castle: a digest of the results,
//                            the count of each (node state, site) pair, and the first castle sites with their height
//   castle:N:S0,S1,S2:L:x,y;x,y;...[:sites]
//                            the castle placement (0x442704, docs/rules/castle.md) on that map at each tile after
//                            the evaluator judged it, for the start stock levels L (decimal, comma separated): the
//                            26 stock words, a digest of each byte of a tile's two records, the tiles that
//                            changed, and the first bytes of the new settlers; with sites also the evaluator's
//                            sweep (as site) for the player who owns the territory, and the flag button's
//                            verdicts on it (as flag)
//   flag:N:S0,S1,S2          the flag button (0x442244, docs/rules/build-sites.md) on every tile of the map gen makes
//                            whose object has a size byte below 3, for a player without a castle: for each pair
//                            (node state, site) the evaluator leaves, how many tiles put a flag (the routine goes to
//                            0x4422c5) and how many refuse it (it goes to 0x44c768)
//   flagput:N:S0,S1,S2:L:cx,cy:x,y;x,y;...
//                            the flag button (0x442244 with the placement 0x4422c5 to its end) on that map after a
//                            castle at cx,cy (0x442704), on each tile in turn without resetting between them: the
//                            outcome (PLACED or REFUSED), the evaluator's pair, the link word the node holds, the
//                            total of resource 1 (+0x4c), and the bytes the tile's attempt changed
//   stockrows:x              the five rows of start stock numbers the placement interpolates (0x442e8a)
//   opponents:S0,S1,S2       FreeGame_RandomizeOpponents (0x4692d4) for a free-game seed: the 20 setting bytes
//   peek:ADDRESS:COUNT       COUNT bytes of the program at the address (hexadecimal), for investigations
//
// Output lines start with "EMU " so that the caller can tell them from Ghidra's log.
//@category OpenSettlers
// simplicity: EmulatorHelper is deprecated for removal in Ghidra 12.1; the pcode emulator API replaces it
// when a Ghidra release drops it.

import java.math.BigInteger;
import java.nio.charset.StandardCharsets;

import ghidra.app.emulator.EmulatorHelper;
import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.lang.Register;

public class EmulateMapStart extends GhidraScript {

    private static final long REGISTERS = 0x20000000L;
    private static final long STATE = 0x20010000L;
    private static final long STACK = 0x20020000L;
    private static final long MACHINE_STACK = 0x20030000L;
    private static final long TEXT = 0x0067408dL;
    private static final long GROUP_TEXT = 0x00674a27L;

    private static final int RNG_0 = 0x212;
    private static final int RNG_1 = 0x214;
    private static final int RNG_2 = 0x216;
    private static final int SEED_0 = 0x364;
    private static final int MAP_SEED_0 = 0x1ee;

    private EmulatorHelper emu;

    @Override
    protected void run() throws Exception {
        for (String command : getScriptArgs()) {
            int colon = command.indexOf(':');
            String name = command.substring(0, colon);
            String rest = command.substring(colon + 1);
            emu = new EmulatorHelper(currentProgram);
            try {
                switch (name) {
                    case "rng" -> rng(rest);
                    case "steps" -> steps(rest);
                    case "parse" -> parse(rest);
                    case "show" -> show(rest);
                    case "mask" -> mask(rest);
                    case "gen" -> gen(rest);
                    case "site" -> site(rest);
                    case "castle" -> castle(rest);
                    case "flag" -> flag(rest);
                    case "flagput" -> flagput(rest);
                    case "stockrows" -> stockrows();
                    case "peek" -> peek(rest);
                    case "opponents" -> opponents(rest);
                    case "tables" -> tables();
                    default -> throw new IllegalArgumentException("unknown command " + command);
                }
            } finally {
                emu.dispose();
            }
        }
    }

    private void prepare() throws Exception {
        emu.writeRegister("EDI", REGISTERS);
        emu.writeRegister("ESP", MACHINE_STACK);
        emu.writeRegister("EBX", STATE);
        putInt(REGISTERS + 0x34, STATE);
        putInt(REGISTERS + 0x3c, STACK);
    }

    private void putInt(long address, long value) throws Exception {
        emu.writeMemory(toAddr(address), new byte[] {(byte) value, (byte) (value >>> 8), (byte) (value >>> 16),
                (byte) (value >>> 24)});
    }

    private void putWord(long address, int value) throws Exception {
        emu.writeMemory(toAddr(address), new byte[] {(byte) value, (byte) (value >>> 8)});
    }

    private int getWord(long address) throws Exception {
        byte[] bytes = emu.readMemory(toAddr(address), 2);
        return (bytes[0] & 0xff) | ((bytes[1] & 0xff) << 8);
    }

    /** Prepares the registers and stores the three seed words in the game state. */
    private void putSeed(int[] seed) throws Exception {
        prepare();
        for (int i = 0; i < 3; i++) {
            putWord(STATE + SEED_0 + 2 * i, seed[i]);
        }
    }

    private int[] words(String list) {
        String[] parts = list.split(",");
        int[] words = new int[parts.length];
        for (int i = 0; i < parts.length; i++) {
            words[i] = Integer.parseInt(parts[i], 16);
        }
        return words;
    }

    /** Runs from start until the program counter reaches one of the stops; returns the stop it reached. */
    private long runTo(long start, long... stops) throws Exception {
        emu.writeRegister(emu.getPCRegister(), start);
        for (long stop : stops) {
            emu.setBreakpoint(toAddr(stop));
        }
        emu.run(monitor);
        if (emu.getLastError() != null) {
            throw new IllegalStateException("emulation failed: " + emu.getLastError());
        }
        BigInteger pc = emu.readRegister(emu.getPCRegister());
        for (long stop : stops) {
            if (pc.longValue() == stop) {
                return stop;
            }
        }
        throw new IllegalStateException("stopped at " + pc.toString(16));
    }

    private static String hex4(int word) {
        return String.format("%04X", word & 0xffff);
    }

    private void step(int[] state) throws Exception {
        putWord(STATE + RNG_0, state[0]);
        putWord(STATE + RNG_1, state[1]);
        putWord(STATE + RNG_2, state[2]);
        runTo(0x466ade, 0x466b93);
        state[0] = getWord(STATE + RNG_0);
        state[1] = getWord(STATE + RNG_1);
        state[2] = getWord(STATE + RNG_2);
    }

    private void rng(String rest) throws Exception {
        int[] state = words(rest);
        prepare();
        step(state);
        println("EMU rng in=" + rest.toUpperCase() + " out=" + hex4(state[0]) + "," + hex4(state[1]) + ","
                + hex4(state[2]));
    }

    private void steps(String rest) throws Exception {
        int colon = rest.indexOf(':');
        int count = Integer.parseInt(rest.substring(0, colon));
        int[] state = words(rest.substring(colon + 1));
        prepare();
        for (int i = 1; i <= count; i++) {
            step(state);
            println("EMU steps n=" + i + " out=" + hex4(state[0]) + "," + hex4(state[1]) + "," + hex4(state[2]));
        }
    }

    private void parse(String rest) throws Exception {
        prepare();
        emu.writeMemory(toAddr(TEXT), rest.getBytes(StandardCharsets.ISO_8859_1));
        long stop = runTo(0x467e12, 0x467f39, 0x467f49);
        if (stop == 0x467f49) {
            println("EMU parse in=" + rest + " REFUSED");
            return;
        }
        println("EMU parse in=" + rest + " words=" + hex4(getWord(STATE + SEED_0)) + ","
                + hex4(getWord(STATE + SEED_0 + 2)) + "," + hex4(getWord(STATE + SEED_0 + 4)));
    }

    private void show(String rest) throws Exception {
        putSeed(words(rest));
        // The four calls at 0x467841, 0x46786b, 0x467899 and 0x4678c1 hand one group each in [EDI+8]. Run each
        // group's call target up to the end of its four character stores instead of through the screen redraw.
        long[] calls = {0x467841, 0x46786b, 0x467899, 0x4678c1};
        long from = 0x467831;
        StringBuilder out = new StringBuilder("EMU show in=" + rest.toUpperCase());
        for (long call : calls) {
            runTo(from, call);
            int group = getWord(REGISTERS + 8);
            runTo(0x4678c7, 0x467941);
            String text = new String(emu.readMemory(toAddr(GROUP_TEXT), 4), StandardCharsets.ISO_8859_1);
            out.append(" group=").append(String.format("%03X", group)).append(":").append(text);
            from = call + 5;
        }
        println(out.toString());
    }

    private void mask(String rest) throws Exception {
        putSeed(words(rest));
        runTo(0x468b3d, 0x468ba8);
        println("EMU mask in=" + rest.toUpperCase() + " out=" + hex4(getWord(STATE + MAP_SEED_0)) + ","
                + hex4(getWord(STATE + MAP_SEED_0 + 2)) + "," + hex4(getWord(STATE + MAP_SEED_0 + 4)));
    }

    private static final long MAP = 0x21000000L;
    private static final long RETURN = 0x41ee3bL;
    private static final int MAP_BUFFER = 0x24;
    private static final int WIDTH = 0x1c2;
    private static final int HEIGHT = 0x1c4;
    private static final int MAP_SEED_WORDS = 0x1ee;

    /** The instruction after each call in MapGen_Generate that ends a recorded step, with the step's name. */
    private static final long[] END_ADDRESS = {0x421749, 0x421761, 0x421774, 0x421779, 0x42178c, 0x42179f,
            0x4217b2, 0x4217c5, 0x4217d8, 0x4217eb,
            // MapGen_PlaceTerrainAndObjects (0x42336d): after each pass it calls, the 15 scatter passes singly
            0x423372, 0x423385, 0x423398, 0x4233ab, 0x4233be, 0x4233d1, 0x4233d6, 0x4233e9, 0x4233ee, 0x423401,
            0x423406, 0x423419, 0x42341e, 0x423423, 0x423428, 0x42343b, 0x423440, 0x423445, 0x42344a, 0x42344f,
            0x423462, 0x423475, 0x423488, 0x42349b, 0x4234ae,
            // MapGen_Generate: after MapGen_MarkWaterAdjacent and MapGen_CountResource1
            0x421803, 0x421808};
    private static final String[] END_NAME = {"Seed", "ClearMap", "SeedHeights", "SubdivideHeights",
            "LimitSlopes", "FillLakes", "LowerToWaterLevel", "ClassifyTerrain", "KeepMainLand", "ScaleHeights",
            "GradeWaterDepth", "SetShoreTerrain", "PlacePatches", "BlendPatchEdges", "MarkPeaks",
            "ScatterObjects1", "ScatterObjects2", "ScatterObjects3", "ScatterObjects4", "ScatterObjects5",
            "ScatterObjects6", "ScatterObjects7", "ScatterObjects8", "ScatterObjects9", "ScatterObjects10",
            "ScatterObjects11", "ScatterObjects12", "ScatterObjects13", "ScatterObjects14", "ScatterObjects15",
            "PlaceDeposits3", "PlaceDeposits2", "PlaceDeposits1", "PlaceDeposits4", "ResolveObjectOverlap",
            "MarkWaterAdjacent", "CountResource1"};
    /** The calls to MapGen_StepProgress (0x4218d8), which draws the progress bar; the harness skips them. */
    private static final long[] SKIP_CALL = {0x421757, 0x42176a, 0x421782, 0x421795, 0x4217a8, 0x4217bb,
            0x4217ce, 0x4217e1, 0x4217f4, 0x42337b, 0x42338e, 0x4233a1, 0x4233b4, 0x4233c7, 0x4233df, 0x4233f7,
            0x42340f, 0x423431, 0x423458, 0x42346b, 0x42347e, 0x423491, 0x4234a4, 0x4234b7};
    /** Game_Init's table of object sizes (0x41e640, set into +0xc8 by 0x41dbea) and the spiral table builder. */
    private static final long OBJECT_SIZES = 0x41e640L;
    private static final int OBJECT_SIZES_POINTER = 0xc8;
    private static final long SPIRAL_BUILDER = 0x41e726L;
    private static final int SPIRAL_POINTER = 0x80;
    private static final int RESOURCE_TOTAL = 0x4c;

    private void gen(String rest) throws Exception {
        generate(rest, true);
    }

    /** Runs the map generation; with report it prints the digest lines, and it returns the width and height. */
    private int[] generate(String rest, boolean report) throws Exception {
        String[] parts = rest.split(":");
        int size = Integer.parseInt(parts[0]);
        int[] seed = words(parts[1]);
        prepare();
        putWord(REGISTERS + 8, size);
        runTo(0x4685cf, 0x468620);
        int width = getWord(STATE + WIDTH);
        int height = getWord(STATE + HEIGHT);
        int bytes = height * 8 * width;
        byte[] fill = new byte[bytes];
        java.util.Arrays.fill(fill, (byte) 0xA5);
        emu.writeMemory(toAddr(MAP), fill);
        putInt(STATE + MAP_BUFFER, MAP);
        call(0x42195c);
        // MapGen_InitParams: only its first two stores (water level, lake radius) are run; it stops at 0x421546.
        runTo(0x421530, 0x421546);
        putInt(STATE + OBJECT_SIZES_POINTER, OBJECT_SIZES);
        call(SPIRAL_BUILDER);
        for (int i = 0; i < 3; i++) {
            putWord(STATE + MAP_SEED_WORDS + 2 * i, seed[i]);
        }
        if (report) {
            println("EMU gen size=" + size + " width=" + width + " height=" + height + " bits="
                    + getWord(STATE + 0x30) + " colmask=" + getWord(STATE + 0x32) + " rowmask="
                    + getWord(STATE + 0x34));
        }
        emu.writeRegister(emu.getPCRegister(), 0x4216e9);
        for (long address : SKIP_CALL) {
            emu.setBreakpoint(toAddr(address));
        }
        for (long address : END_ADDRESS) {
            emu.setBreakpoint(toAddr(address));
        }
        int recorded = 0;
        while (recorded < END_ADDRESS.length) {
            emu.run(monitor);
            if (emu.getLastError() != null) {
                throw new IllegalStateException("emulation failed: " + emu.getLastError());
            }
            long pc = emu.readRegister(emu.getPCRegister()).longValue();
            if (pc == END_ADDRESS[recorded]) {
                if (report) {
                    digest(END_NAME[recorded], size, width, height);
                }
                recorded++;
                if (recorded < END_ADDRESS.length) {
                    emu.step(monitor);
                }
            } else {
                emu.writeRegister(emu.getPCRegister(), pc + 5);
            }
        }
        return new int[] {size, width, height};
    }

    /** Runs the function at entry to its return: pushes a return address on the machine stack and stops there. */
    private void call(long entry) throws Exception {
        long esp = emu.readRegister("ESP").longValue() - 4;
        emu.writeRegister("ESP", esp);
        putInt(esp, RETURN);
        runTo(entry, RETURN);
    }

    private void digest(String pass, int size, int width, int height) throws Exception {
        String rng = " rng=" + hex4(getWord(STATE + RNG_0)) + "," + hex4(getWord(STATE + RNG_1)) + ","
                + hex4(getWord(STATE + RNG_2));
        if (pass.equals("Seed")) {
            println("EMU gen size=" + size + " pass=" + pass + rng);
            return;
        }
        byte[] map = emu.readMemory(toAddr(MAP), height * 8 * width);
        long[] hash = new long[6];
        java.util.Arrays.fill(hash, 0xcbf29ce484222325L);
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int a = y * 8 * width + x * 4;
                int b = a + 4 * width;
                // flags, height, terrain, lake, object (record A byte 3), resource (record B byte 0, as the lake)
                int[] at = {a, a + 1, a + 2, b, a + 3, b};
                for (int layer = 0; layer < 6; layer++) {
                    hash[layer] = (hash[layer] ^ (map[at[layer]] & 0xff)) * 0x100000001b3L;
                }
                // The rules note that no pass writes bytes 1 to 3 of record B (MapGen_PlaceDeposits reads byte 1).
                if (map[b + 1] != 0 || map[b + 2] != 0 || map[b + 3] != 0) {
                    throw new IllegalStateException("record B holds more than byte 0 at " + x + "," + y);
                }
            }
        }
        String total = "";
        if (pass.equals("CountResource1")) {
            total = " total=" + (getWord(STATE + RESOURCE_TOTAL) | (getWord(STATE + RESOURCE_TOTAL + 2) << 16));
        }
        println("EMU gen size=" + size + " pass=" + pass + rng
                + String.format(" flags=%016X height=%016X terrain=%016X lake=%016X object=%016X resource=%016X",
                        hash[0], hash[1], hash[2], hash[3], hash[4], hash[5])
                + total);
    }

    private static final long PLAYER = 0x22000000L;
    private static final long OFFSETS = 0x21800000L;
    private static final long TABLE18 = 0x23000000L;
    private static final int OFFSETS_POINTER = 0xc4;
    private static final int TABLE18_BASE = 0x9c;

    /**
     * The build-site evaluator (0x44b6e6) for a player without a castle on every tile of a generated map. The
     * command is site:N:S0,S1,S2 (the arguments of gen). A tile whose object has a size byte of 3 or more is
     * counted as skipped: the evaluator then reads the building and flag tables, which hold nothing here. The
     * other tiles give the triple the evaluator leaves in the player block (+0x100, +0x101, +0x102); the line
     * prints their FNV-1a digest in row-major order, the number of each (kind, site) pair, and the first valid
     * castle sites (kind 7, site 5) with the height the evaluator hands on.
     */
    private void site(String rest) throws Exception {
        int[] size = generate(rest, false);
        int width = size[1];
        int height = size[2];
        putInt(STATE + OFFSETS_POINTER, OFFSETS);
        putInt(STATE + TABLE18_BASE, TABLE18);
        call(0x41f6a5);
        putInt(REGISTERS + 0x30, PLAYER);
        println("EMU site size=" + size[0] + " width=" + width + " height=" + height + sweep(width, height, PLAYER, true, 5));
    }

    /**
     * Runs the evaluator (0x44b6e6) on every tile whose object has a size byte below 3 and describes the results:
     * the count of tiles judged and skipped, the number with the site value validSite (the first 16 are listed
     * with their height), the FNV-1a digest of the results in row-major order, and the count of each pair. With
     * fresh the player block is cleared before each tile (a player without a castle); otherwise it is left as
     * it is, apart from the position.
     */
    private String sweep(int width, int height, long player, boolean fresh, int validSite) throws Exception {
        byte[] sizes = emu.readMemory(toAddr(OBJECT_SIZES), 128);
        long hash = 0xcbf29ce484222325L;
        int evaluated = 0;
        int skipped = 0;
        int valid = 0;
        java.util.TreeMap<String, Integer> kinds = new java.util.TreeMap<>();
        StringBuilder first = new StringBuilder();
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int object = emu.readMemory(toAddr(MAP + y * 8L * width + x * 4L + 3), 1)[0] & 0x7f;
                if ((sizes[object] & 0xff) >= 3) {
                    skipped++;
                    continue;
                }
                if (fresh) {
                    emu.writeMemory(toAddr(player), new byte[0x200]);
                }
                putWord(player + 0xfc, x);
                putWord(player + 0xfe, y);
                call(0x44b6e6);
                byte[] result = emu.readMemory(toAddr(player + 0x100), 4);
                int kind = result[0] & 0xff;
                int site = result[1] & 0xff;
                int level = (result[2] & 0xff) | ((result[3] & 0xff) << 8);
                if (site < 4) {
                    // The evaluator writes +0x102 only for the sites 4 and 5; an older value stays there.
                    level = 0;
                }
                for (int b : new int[] {kind, site, level & 0xff, level >> 8}) {
                    hash = (hash ^ b) * 0x100000001b3L;
                }
                evaluated++;
                kinds.merge(kind + "." + site, 1, Integer::sum);
                if (site == validSite && (kind == 7 || !fresh)) {
                    if (valid < 16) {
                        first.append(" ").append(x).append(",").append(y).append(",").append(level);
                    }
                    valid++;
                }
            }
        }
        StringBuilder histogram = new StringBuilder();
        kinds.forEach((key, count) -> histogram.append(" ").append(key).append(":").append(count));
        return " evaluated=" + evaluated + " skipped=" + skipped + " valid=" + valid
                + String.format(" digest=%016X", hash) + " kinds=" + histogram.toString().trim() + " first="
                + first.toString().trim();
    }

    private static final long RECORD = 0x24000000L;

    /**
     * The flag button (0x442244) on every tile whose object has a size byte below 3, for a player without a
     * castle. The command is flag:N:S0,S1,S2 (the arguments of gen). The routine reads the tile record through the
     * pointer at +0x20 of its state and the player through the pointer at +0x82 of that record; it runs the
     * evaluator and then goes to 0x4422c5 (the flag is put) or to 0x44c768 (it is refused). The run stops at
     * either, so that the placing routines do not run. The line gives, per pair (node state, site) the evaluator
     * left, the tiles that put a flag over the tiles judged.
     */
    private void flag(String rest) throws Exception {
        int[] size = generate(rest, false);
        int width = size[1];
        int height = size[2];
        putInt(STATE + OFFSETS_POINTER, OFFSETS);
        putInt(STATE + TABLE18_BASE, TABLE18);
        call(0x41f6a5);
        println("EMU flag size=" + size[0] + " width=" + width + " height=" + height
                + flagSweep(width, height, PLAYER, true));
    }

    /**
     * Runs the flag button on every tile whose object has a size byte below 3 and counts, per pair (node state,
     * site) the evaluator left, the tiles that put a flag over the tiles judged. With fresh the player block is
     * cleared before each tile, otherwise it is left as it is apart from the position.
     */
    private String flagSweep(int width, int height, long player, boolean fresh) throws Exception {
        byte[] sizes = emu.readMemory(toAddr(OBJECT_SIZES), 128);
        java.util.TreeMap<String, int[]> pairs = new java.util.TreeMap<>();
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int object = emu.readMemory(toAddr(MAP + y * 8L * width + x * 4L + 3), 1)[0] & 0x7f;
                if ((sizes[object] & 0xff) >= 3) {
                    continue;
                }
                if (fresh) {
                    emu.writeMemory(toAddr(player), new byte[0x200]);
                }
                emu.writeMemory(toAddr(RECORD), new byte[0x100]);
                putInt(RECORD + 0x82, player);
                putInt(REGISTERS + 0x20, RECORD);
                putInt(REGISTERS + 0x30, player);
                putWord(player + 0xfc, x);
                putWord(player + 0xfe, y);
                long esp = emu.readRegister("ESP").longValue() - 4;
                emu.writeRegister("ESP", esp);
                putInt(esp, RETURN);
                long stop = runTo(0x442244, 0x4422c5, 0x44c768);
                emu.writeRegister("ESP", esp + 4);
                byte[] result = emu.readMemory(toAddr(player + 0x100), 2);
                String key = (result[0] & 0xff) + "." + (result[1] & 0xff);
                pairs.computeIfAbsent(key, k -> new int[2])[stop == 0x4422c5 ? 0 : 1]++;
            }
        }
        StringBuilder text = new StringBuilder();
        pairs.forEach((key, counts) -> text.append(" ").append(key).append(":").append(counts[0]).append("/")
                .append(counts[0] + counts[1]));
        return " pairs=" + text.toString().trim();
    }

    /**
     * The flag button with the placement behind it (flagput:N:S0,S1,S2:L:cx,cy:x,y;x,y;...). It sets the game up as
     * castle: does, places the castle at cx,cy, and then runs the flag button (0x442244) for the player on each
     * tile in turn, keeping the map and the tables between tiles, so a later tile sees the flags of the earlier
     * ones. A tile the button hands to 0x44c768 (the error report) is REFUSED and changes nothing; otherwise the
     * placement runs to its end. Per tile it prints the evaluator's pair, the link word of the node, the total of
     * resource 1 (+0x4c), and the map bytes the attempt changed (the positions as castle-changes gives them).
     */
    private void flagput(String rest) throws Exception {
        String[] parts = rest.split(":");
        int[] size = generate(parts[0] + ":" + parts[1], false);
        int width = size[1];
        int height = size[2];
        int bytes = height * 8 * width;
        putInt(STATE + OFFSETS_POINTER, OFFSETS);
        call(0x41f6a5);
        putInt(STATE + 0x352, 2);
        putInt(STATE + 0x64, PLAYERS + 0x1000);
        putInt(STATE + 0x68, PLAYERS + 0x3000);
        putInt(STATE + 0x6c, PLAYERS + 0x5000);
        putInt(STATE + 0x70, PLAYERS + 0x7000);
        putInt(STATE + 0x78, PLAYERS + 0x9000);
        putInt(STATE + 0x7c, PLAYERS + 0x9400);
        putInt(STATE + 0x98, TABLES);
        putInt(STATE + 0xa4, TABLES + 0x10000);
        putInt(STATE + 0x9c, TABLES + 0x20000);
        putInt(STATE + 0xa8, TABLES + 0x30000);
        putInt(STATE + 0xa0, TABLES + 0x40000);
        putInt(STATE + 0xac, TABLES + 0x50000);
        putInt(STATE + 0xf0, TABLES + 0x60000);
        putInt(STATE + 0xf4, TABLES + 0x70000);
        zero(PLAYERS, 0xa000);
        zero(TABLES, 0x80000);
        for (int i = 0; i < 4; i++) {
            putWord(PLAYERS + 0x1000 + 0x2000 * i, i);
        }
        putWord(STATE + 0x258, 100);
        putWord(STATE + 0x25a, 100);
        putWord(STATE + 0x25c, 100);
        putWord(STATE + 0x264, 100);
        for (int offset : new int[] {0x25e, 0x260, 0x262, 0x266}) {
            putWord(STATE + offset, 0);
        }
        long player = PLAYERS + 0x1000;
        putInt(REGISTERS + 0x30, player);
        String[] centre = parts[3].split(",");
        putWord(player + 0xfc, Integer.parseInt(centre[0]));
        putWord(player + 0xfe, Integer.parseInt(centre[1]));
        emu.writeMemory(toAddr(player + LEVEL), new byte[] {(byte) Integer.parseInt(parts[2])});
        call(0x44b6e6);
        byte[] first = emu.readMemory(toAddr(player + 0x100), 2);
        if ((first[0] & 0xff) != 7 || (first[1] & 0xff) != 5) {
            println("EMU flagput castle=" + parts[3] + " REFUSED");
            return;
        }
        call(0x442704);
        for (String tile : parts[4].split(";")) {
            String[] xy = tile.split(",");
            int x = Integer.parseInt(xy[0]);
            int y = Integer.parseInt(xy[1]);
            byte[] before = emu.readMemory(toAddr(MAP), bytes).clone();
            emu.writeMemory(toAddr(RECORD), new byte[0x100]);
            putInt(RECORD + 0x82, player);
            putInt(REGISTERS + 0x20, RECORD);
            putInt(REGISTERS + 0x30, player);
            putWord(player + 0xfc, x);
            putWord(player + 0xfe, y);
            emu.writeMemory(toAddr(player + 0x100), new byte[4]);
            long esp = emu.readRegister("ESP").longValue() - 4;
            emu.writeRegister("ESP", esp);
            putInt(esp, RETURN);
            long stop = runTo(0x442244, RETURN, 0x44c768);
            emu.writeRegister("ESP", esp + 4);
            byte[] result = emu.readMemory(toAddr(player + 0x100), 2);
            byte[] after = emu.readMemory(toAddr(MAP), bytes);
            StringBuilder changed = new StringBuilder();
            for (int ty = 0; ty < height; ty++) {
                for (int tx = 0; tx < width; tx++) {
                    int a = ty * 8 * width + tx * 4;
                    int b = a + 4 * width;
                    StringBuilder diff = new StringBuilder();
                    for (int i = 0; i < 8; i++) {
                        int at = (i < 4 ? a : b) + (i & 3);
                        if (after[at] != before[at]) {
                            diff.append("/").append(i).append("=").append(String.format("%02X>%02X",
                                    before[at] & 0xff, after[at] & 0xff));
                        }
                    }
                    if (diff.length() > 0) {
                        changed.append(" ").append(tx).append(",").append(ty).append(":").append(diff.substring(1));
                    }
                }
            }
            int b = y * 8 * width + 4 * width + x * 4;
            println("EMU flagput size=" + size[0] + " castle=" + parts[3] + " level=" + parts[2] + " tile=" + x + ","
                    + y + (stop == RETURN ? " PLACED" : " REFUSED") + " evaluator=" + (result[0] & 0xff) + "."
                    + (result[1] & 0xff) + " link=" + ((after[b] & 0xff) | ((after[b + 1] & 0xff) << 8))
                    + " resource1=" + getWord(STATE + RESOURCE_TOTAL) + " changes=" + changed.toString().trim());
        }
    }

    /** peek:ADDRESS:COUNT prints COUNT bytes of the program at the address, for an investigation at the console. */
    private void peek(String rest) throws Exception {
        String[] parts = rest.split(":");
        long address = Long.parseLong(parts[0], 16);
        StringBuilder values = new StringBuilder();
        for (byte v : emu.readMemory(toAddr(address), Integer.parseInt(parts[1]))) {
            values.append(String.format("%02X", v & 0xff));
        }
        println("EMU peek " + parts[0] + " " + values);
    }

    /** The five rows of 26 start stock values that the castle placement interpolates between (0x442e8a). */
    private void stockrows() throws Exception {
        for (int row = 0; row < 5; row++) {
            StringBuilder values = new StringBuilder();
            for (byte v : emu.readMemory(toAddr(0x442e8aL + 26 * row), 26)) {
                values.append(values.length() == 0 ? "" : ",").append(v & 0xff);
            }
            println("EMU stockrows row=" + row + " values=" + values);
        }
    }

    /**
     * FreeGame_RandomizeOpponents (0x4692d4) for a free-game seed (the three words at +0x364): prints the 20
     * setting bytes it writes at +0x36a to +0x37d. The human player's start stock level is the byte at +0x37a.
     */
    private void opponents(String rest) throws Exception {
        int[] seed = words(rest);
        putSeed(seed);
        call(0x4692d4);
        StringBuilder values = new StringBuilder();
        for (byte v : emu.readMemory(toAddr(STATE + 0x36a), 20)) {
            values.append(values.length() == 0 ? "" : ",").append(v & 0xff);
        }
        println("EMU opponents seed=" + rest.toUpperCase() + " settings=" + values);
    }

    private static final long PLAYERS = 0x22000000L;
    private static final long TABLES = 0x23000000L;
    private static final int LEVEL = 0x162;

    private void zero(long address, int length) throws Exception {
        emu.writeMemory(toAddr(address), new byte[length]);
    }

    /**
     * The castle placement (0x442704, the routine of the mission start that the click handler 0x442630 repeats
     * after its check) for a player without a castle. The command is castle:N:S0,S1,S2:L:x,y;x,y;... with the
     * arguments of gen, the start stock level L (player byte +0x162, decimal) and the tiles to try, each from
     * the generated map alone. Per tile it runs the evaluator (0x44b6e6); a tile it does not give the pair
     * (7, 5) is REFUSED and nothing else runs. Otherwise it runs the placement and prints the 26 stock words of
     * the storage item, and the FNV-1a digests of the map bytes by position in the two records of a tile, and the
     * tiles whose bytes changed with their old and new values.
     */
    private void castle(String rest) throws Exception {
        String[] parts = rest.split(":");
        int[] size = generate(parts[0] + ":" + parts[1], false);
        int width = size[1];
        int height = size[2];
        int bytes = height * 8 * width;
        byte[] before = emu.readMemory(toAddr(MAP), bytes).clone();
        putInt(STATE + OFFSETS_POINTER, OFFSETS);
        call(0x41f6a5);
        putInt(STATE + 0x352, 2);
        putInt(STATE + 0x64, PLAYERS + 0x1000);
        putInt(STATE + 0x68, PLAYERS + 0x3000);
        putInt(STATE + 0x6c, PLAYERS + 0x5000);
        putInt(STATE + 0x70, PLAYERS + 0x7000);
        putInt(STATE + 0x78, PLAYERS + 0x9000);
        putInt(STATE + 0x7c, PLAYERS + 0x9400);
        putInt(STATE + 0x98, TABLES);
        putInt(STATE + 0xa4, TABLES + 0x10000);
        putInt(STATE + 0x9c, TABLES + 0x20000);
        putInt(STATE + 0xa8, TABLES + 0x30000);
        putInt(STATE + 0xa0, TABLES + 0x40000);
        putInt(STATE + 0xac, TABLES + 0x50000);
        putInt(STATE + 0xf0, TABLES + 0x60000);
        putInt(STATE + 0xf4, TABLES + 0x70000);
        putInt(REGISTERS + 0x30, PLAYERS + 0x1000);
        byte[] stateBefore = emu.readMemory(toAddr(STATE), 0x1000).clone();
        for (String levelText : parts[2].split(",")) {
        int level = Integer.parseInt(levelText);
        for (String tile : parts[3].split(";")) {
            String[] xy = tile.split(",");
            int x = Integer.parseInt(xy[0]);
            int y = Integer.parseInt(xy[1]);
            emu.writeMemory(toAddr(STATE), stateBefore.clone());
            emu.writeMemory(toAddr(MAP), before.clone());
            zero(PLAYERS, 0xa000);
            zero(TABLES, 0x80000);
            for (int i = 0; i < 4; i++) {
                putWord(PLAYERS + 0x1000 + 0x2000 * i, i);
            }
            putWord(STATE + 0x258, 100);
            putWord(STATE + 0x25a, 100);
            putWord(STATE + 0x25c, 100);
            putWord(STATE + 0x264, 100);
            for (int offset : new int[] {0x25e, 0x260, 0x262, 0x266}) {
                putWord(STATE + offset, 0);
            }
            long player = PLAYERS + 0x1000;
            putWord(player + 0xfc, x);
            putWord(player + 0xfe, y);
            emu.writeMemory(toAddr(player + LEVEL), new byte[] {(byte) level});
            call(0x44b6e6);
            byte[] result = emu.readMemory(toAddr(player + 0x100), 4);
            int kind = result[0] & 0xff;
            int site = result[1] & 0xff;
            int found = (result[2] & 0xff) | ((result[3] & 0xff) << 8);
            if (kind != 7 || site != 5) {
                println("EMU castle size=" + size[0] + " tile=" + x + "," + y + " level=" + level + " evaluator="
                        + kind + "." + site + "." + found + " REFUSED");
                continue;
            }
            call(0x442704);
            StringBuilder stock = new StringBuilder();
            long item120 = TABLES + 0x60000;
            for (int i = 0; i < 26; i++) {
                stock.append(i == 0 ? "" : ",").append(getWord(item120 + 6 + 2 * i));
            }
            byte[] after = emu.readMemory(toAddr(MAP), bytes).clone();
            long[] hash = new long[8];
            java.util.Arrays.fill(hash, 0xcbf29ce484222325L);
            StringBuilder changed = new StringBuilder();
            int changes = 0;
            for (int ty = 0; ty < height; ty++) {
                for (int tx = 0; tx < width; tx++) {
                    int a = ty * 8 * width + tx * 4;
                    int b = a + 4 * width;
                    StringBuilder diff = new StringBuilder();
                    for (int i = 0; i < 8; i++) {
                        int at = (i < 4 ? a : b) + (i & 3);
                        hash[i] = (hash[i] ^ (after[at] & 0xff)) * 0x100000001b3L;
                        if (after[at] != before[at]) {
                            diff.append(" ").append(i).append("=").append(String.format("%02X>%02X",
                                    before[at] & 0xff, after[at] & 0xff));
                        }
                    }
                    if (diff.length() > 0) {
                        changes++;
                        changed.append(" ").append(tx).append(",").append(ty).append(":").append(diff.toString()
                                .trim().replace(" ", "/"));
                    }
                }
            }
            StringBuilder digests = new StringBuilder();
            for (int i = 0; i < 8; i++) {
                digests.append(String.format(" %016X", hash[i]));
            }
            println("EMU castle size=" + size[0] + " tile=" + x + "," + y + " level=" + level + " evaluator="
                    + kind + "." + site + "." + found + " stock=" + stock + " layers=" + digests.toString().trim()
                    + " changes=" + changes);
            println("EMU castle-changes tile=" + x + "," + y + changed);
            if (parts.length > 4) {
                // The evaluator for the player now that the castle stands, on every tile (site 4 is a large building).
                println("EMU castle-sites tile=" + x + "," + y + " level=" + level + sweep(width, height, player, false, 4));
                println("EMU castle-flags tile=" + x + "," + y + " level=" + level + flagSweep(width, height, player, false));
            }
            StringBuilder settlers = new StringBuilder();
            int created = getWord(STATE + 0x262);
            for (int i = 0; i < created; i++) {
                settlers.append(" ").append(String.format("%02X", emu.readMemory(toAddr(TABLES + 0x40000 + 16L * i), 1)[0] & 0xff));
            }
            println("EMU castle-settlers tile=" + x + "," + y + " level=" + level + " count=" + created
                    + " first-bytes=" + settlers.toString().trim() + " player=" + String.format("b2=%02X b3=%02X p108=%d p116=%d",
                    emu.readMemory(toAddr(player + 2), 1)[0] & 0xff, emu.readMemory(toAddr(player + 3), 1)[0] & 0xff,
                    getWord(player + 0x108), getWord(player + 0x116)));
            }
        }
    }

    /** Builds the spiral table and prints its entries and the object size table (a small view, not the bytes). */
    private void tables() throws Exception {
        prepare();
        putInt(STATE + OBJECT_SIZES_POINTER, OBJECT_SIZES);
        call(SPIRAL_BUILDER);
        long pointer = 0;
        byte[] raw = emu.readMemory(toAddr(STATE + SPIRAL_POINTER), 4);
        for (int i = 3; i >= 0; i--) {
            pointer = (pointer << 8) | (raw[i] & 0xff);
        }
        // The passes index at most 255 (a draw masked with 0xff, or a count of 256); the later entries are not used.
        int entries = 256;
        byte[] bytes = emu.readMemory(toAddr(pointer), entries * 2);
        long hash = 0xcbf29ce484222325L;
        StringBuilder head = new StringBuilder();
        for (int i = 0; i < bytes.length; i++) {
            hash = (hash ^ (bytes[i] & 0xff)) * 0x100000001b3L;
        }
        // Entry 0 is the center; each later group of six entries is the six rotations of its first entry (the seed).
        for (int i = 0; i < 43; i++) {
            head.append(" ").append(bytes[2 * (6 * i + 1)]).append(",").append(bytes[2 * (6 * i + 1) + 1]);
        }
        println("EMU tables spiral=" + String.format("%016X", hash) + " seeds=" + head);
        byte[] sizes = emu.readMemory(toAddr(OBJECT_SIZES), 128);
        StringBuilder large = new StringBuilder();
        for (int i = 0; i < 128; i++) {
            if ((sizes[i] & 0xff) > 1) {
                large.append(" ").append(i);
            }
        }
        println("EMU tables large=" + large);
        StringBuilder runs = new StringBuilder();
        for (int first = 0; first < 128; ) {
            int last = first;
            while (last + 1 < 128 && sizes[last + 1] == sizes[first]) {
                last++;
            }
            runs.append(" ").append(first).append("-").append(last).append("=").append(sizes[first] & 0xff);
            first = last + 1;
        }
        println("EMU tables sizeruns=" + runs.toString().trim());
    }
}
