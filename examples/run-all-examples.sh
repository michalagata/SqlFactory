#!/bin/bash
# Run all SQLFactory examples and verify they execute successfully

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "═══════════════════════════════════════════════════════════════"
echo "  SQLFactory Examples - Test Runner"
echo "  Testing all 12 example projects"
echo "═══════════════════════════════════════════════════════════════"
echo ""

PASS=0
FAIL=0
SKIPPED=0

run_example() {
    local example_name="$1"
    local example_dir="$SCRIPT_DIR/$example_name"
    
    if [ ! -d "$example_dir" ]; then
        echo "⊘ $example_name: SKIPPED (directory not found)"
        ((SKIPPED++))
        return
    fi
    
    if [ ! -f "$example_dir/Program.cs" ] && [ ! -f "$example_dir/README.md" ]; then
        echo "⊘ $example_name: SKIPPED (no Program.cs or README.md)"
        ((SKIPPED++))
        return
    fi
    
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "🧪 Testing: $example_name"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    
    cd "$example_dir"
    
    # CodeGeneration is README-only
    if [ "$example_name" = "CodeGeneration" ]; then
        if [ -f "README.md" ]; then
            echo "✓ $example_name: PASSED (README documentation)"
            ((PASS++))
        else
            echo "✗ $example_name: FAILED (README missing)"
            ((FAIL++))
        fi
        cd "$SCRIPT_DIR"
        return
    fi
    
    # Build and run
    if dotnet build --nologo -v quiet > /dev/null 2>&1; then
        echo "  ✓ Build succeeded"
        
        if timeout 30 dotnet run --no-build --nologo > /dev/null 2>&1; then
            echo "✓ $example_name: PASSED"
            ((PASS++))
        else
            echo "✗ $example_name: FAILED (runtime error or timeout)"
            ((FAIL++))
        fi
    else
        echo "✗ $example_name: FAILED (build error)"
        ((FAIL++))
    fi
    
    cd "$SCRIPT_DIR"
    echo ""
}

# Run all examples
run_example "BasicCRUD"
run_example "AdvancedQuerying"
run_example "EagerLoading"
run_example "LazyLoading"
run_example "GlobalFilters"
run_example "ChangeTracking"
run_example "ReadWriteSplitting"
run_example "SoftDelete"
run_example "Caching"
run_example "BulkOperations"
run_example "CodeGeneration"
run_example "FullStackApp"

# Summary
echo "═══════════════════════════════════════════════════════════════"
echo "  Test Summary"
echo "═══════════════════════════════════════════════════════════════"
echo "  ✓ Passed:  $PASS"
echo "  ✗ Failed:  $FAIL"
echo "  ⊘ Skipped: $SKIPPED"
echo "═══════════════════════════════════════════════════════════════"

if [ $FAIL -gt 0 ]; then
    echo ""
    echo "❌ Some examples failed. Review output above for details."
    exit 1
else
    echo ""
    echo "✅ All examples passed successfully!"
    exit 0
fi
