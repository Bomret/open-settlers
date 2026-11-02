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
//                            MapGen_InitParams (0x421530) to the end of MapGen_ScaleHeights. Prints the
//                            geometry, then after the seeding the generator words, and after each pass the
//                            generator words and one FNV-1a 64-bit digest per layer of the canonical map form
//                            (docs/rules/map-form.md)
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
            0x4217b2, 0x4217c5, 0x4217d8, 0x4217eb};
    private static final String[] END_NAME = {"Seed", "ClearMap", "SeedHeights", "SubdivideHeights",
            "LimitSlopes", "FillLakes", "LowerToWaterLevel", "ClassifyTerrain", "KeepMainLand", "ScaleHeights"};
    /** The calls to MapGen_StepProgress (0x4218d8), which draws the progress bar; the harness skips them. */
    private static final long[] SKIP_CALL = {0x421757, 0x42176a, 0x421782, 0x421795, 0x4217a8, 0x4217bb,
            0x4217ce, 0x4217e1};

    private void gen(String rest) throws Exception {
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
        runTo(0x421530, 0x421546);
        for (int i = 0; i < 3; i++) {
            putWord(STATE + MAP_SEED_WORDS + 2 * i, seed[i]);
        }
        println("EMU gen size=" + size + " width=" + width + " height=" + height);
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
                digest(END_NAME[recorded], size, width, height);
                recorded++;
                if (recorded < END_ADDRESS.length) {
                    emu.step(monitor);
                }
            } else {
                emu.writeRegister(emu.getPCRegister(), pc + 5);
            }
        }
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
        long[] hash = {0xcbf29ce484222325L, 0xcbf29ce484222325L, 0xcbf29ce484222325L, 0xcbf29ce484222325L};
        for (int y = 0; y < height; y++) {
            for (int x = 0; x < width; x++) {
                int a = y * 8 * width + x * 4;
                int b = a + 4 * width;
                int[] at = {a, a + 1, a + 2, b};
                for (int layer = 0; layer < 4; layer++) {
                    hash[layer] = (hash[layer] ^ (map[at[layer]] & 0xff)) * 0x100000001b3L;
                }
            }
        }
        println("EMU gen size=" + size + " pass=" + pass + rng
                + String.format(" flags=%016X height=%016X terrain=%016X lake=%016X", hash[0], hash[1], hash[2],
                        hash[3]));
    }
}
