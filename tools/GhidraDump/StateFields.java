// Writes state_fields.tsv: for each offset into the game state block, the routines that read it and the
// routines that write it. The game code loads the state block pointer from offset +0x34 of the EDI block
// into a register and accesses fields as [register + offset]. The script follows that register through
// straight-line code: it adds an ADD of a constant to the register, forgets the register when another
// instruction writes it, and forgets all registers after a return or an unconditional jump. Accesses
// that pass through other registers or across branches are missed. Run headless (see README.md); the
// first script argument is the output folder.
//@category OpenSettlers

import java.io.File;
import java.io.PrintWriter;
import java.util.HashMap;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;
import java.util.TreeSet;

import ghidra.app.script.GhidraScript;
import ghidra.program.model.address.Address;
import ghidra.program.model.lang.OperandType;
import ghidra.program.model.lang.Register;
import ghidra.program.model.listing.Function;
import ghidra.program.model.listing.Instruction;
import ghidra.program.model.listing.InstructionIterator;
import ghidra.program.model.scalar.Scalar;
import ghidra.program.model.symbol.RefType;

public class StateFields extends GhidraScript {

    private static final long BAND_START = 0x41da10L;
    private static final long BAND_END = 0x47b739L;
    private static final long STATE_POINTER = 0x34;

    private final Map<Long, Set<String>> readers = new TreeMap<>();
    private final Map<Long, Set<String>> writers = new TreeMap<>();
    private final Map<Long, Set<String>> sizes = new TreeMap<>();

    @Override
    protected void run() throws Exception {
        File out = new File(getScriptArgs()[0]);
        Register edi = currentProgram.getRegister("EDI");
        Map<Register, Long> state = new HashMap<>();

        InstructionIterator it = currentProgram.getListing().getInstructions(toAddr(BAND_START), true);
        while (it.hasNext() && !monitor.isCancelled()) {
            Instruction instruction = it.next();
            long at = instruction.getAddress().getOffset();
            if (at > BAND_END) {
                break;
            }
            if (getFunctionAt(instruction.getAddress()) != null) {
                state.clear();
            }

            for (int i = 0; i < instruction.getNumOperands(); i++) {
                Register base = memoryBase(instruction, i);
                if (base != null && state.containsKey(base)) {
                    long offset = state.get(base) + displacement(instruction, i);
                    record(instruction, i, offset);
                }
            }

            String mnemonic = instruction.getMnemonicString();
            Register target = instruction.getNumOperands() > 0 ? instruction.getRegister(0) : null;
            Register target32 = target == null ? null : target.getBaseRegister();
            if (mnemonic.equals("MOV") && target32 != null && target.getBitLength() == 32
                && edi.equals(memoryBase(instruction, 1)) && displacement(instruction, 1) == STATE_POINTER) {
                state.put(target32, 0L);
            } else if (mnemonic.equals("ADD") && target32 != null && state.containsKey(target32)
                && instruction.getScalar(1) != null) {
                state.put(target32, state.get(target32) + instruction.getScalar(1).getSignedValue());
            } else {
                for (Object result : instruction.getResultObjects()) {
                    if (result instanceof Register r) {
                        state.remove(r.getBaseRegister());
                    }
                }
            }
            if (instruction.getFlowType().isTerminal()
                || (instruction.getFlowType().isJump() && instruction.getFlowType().isUnConditional())) {
                state.clear();
            }
        }

        try (PrintWriter w = new PrintWriter(new File(out, "state_fields.tsv"), "UTF-8")) {
            w.println("offset\tsizes\treaders\twriters");
            Set<Long> offsets = new TreeSet<>(readers.keySet());
            offsets.addAll(writers.keySet());
            for (long offset : offsets) {
                w.printf("+0x%x\t%s\t%s\t%s%n", offset, String.join(",", sizes.getOrDefault(offset, Set.of())),
                    String.join(",", readers.getOrDefault(offset, Set.of())),
                    String.join(",", writers.getOrDefault(offset, Set.of())));
            }
        }
        println("StateFields: " + (readers.size() + writers.size()) + " entries");
    }

    private void record(Instruction instruction, int operand, long offset) {
        String who = routine(instruction.getAddress());
        RefType type = instruction.getOperandRefType(operand);
        if (type.isWrite()) {
            writers.computeIfAbsent(offset, k -> new TreeSet<>()).add(who);
        }
        if (type.isRead() || !type.isWrite()) {
            readers.computeIfAbsent(offset, k -> new TreeSet<>()).add(who);
        }
        String representation = instruction.getDefaultOperandRepresentation(operand);
        int ptr = representation.indexOf(" ptr");
        if (ptr > 0) {
            sizes.computeIfAbsent(offset, k -> new TreeSet<>()).add(representation.substring(0, ptr));
        }
    }

    private String routine(Address address) {
        Function f = getFunctionContaining(address);
        if (f == null) {
            return "0x" + address;
        }
        String entry = "0x" + f.getEntryPoint();
        return f.getName().startsWith("FUN_") ? entry : f.getName() + "@" + entry;
    }

    private static Register memoryBase(Instruction instruction, int operand) {
        if ((instruction.getOperandType(operand) & OperandType.DYNAMIC) == 0) {
            return null;
        }
        Register base = null;
        for (Object o : instruction.getOpObjects(operand)) {
            if (o instanceof Register r) {
                if (base != null) {
                    return null;
                }
                base = r.getBaseRegister();
            }
        }
        return base;
    }

    private static long displacement(Instruction instruction, int operand) {
        for (Object o : instruction.getOpObjects(operand)) {
            if (o instanceof Scalar s) {
                return s.getSignedValue();
            }
        }
        return 0;
    }
}
