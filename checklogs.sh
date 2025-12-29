pushd "C:\Users\Tatu\AppData\Roaming\SpaceEngineers"
outp="E:\gitrepos\AHOD\lastlog.log"
f=$(printf "%s\n" SpaceEngineers_* | sort | tail -n1)
if [ -f "$f" ]; then
    # Disable grep colors and group by 5-hex context codes (e.g. [076fd]).
    # Insert an empty line whenever the context code changes. Lines without
    # a context are treated as a single shared context so they remain grouped.
    # Also strip the YYYY-MM-DD part of the timestamp and the Thread token.
    tail -n500 "$f" | grep --color=never "AHOD" | \
    awk 'BEGIN{prev=""}
    {
        if (match($0, /\[([0-9A-Fa-f]{5})\]/, m)) key=m[1]; else key="__NOCTX__";
        if (NR>1 && key!=prev) print "";
        line=$0;
        sub(/^[0-9]{4}-[0-9]{2}-[0-9]{2} /, "", line); # remove date
        sub(/ - Thread:[ \t]*[0-9]+ ->[ \t]*/, " ", line); # remove thread marker
        gsub(/^[ \t]+|[ \t]+$/, "", line); # trim
        print line;
        prev=key;
    }' > "$outp"
    cat "$outp"
else
    echo "No SpaceEngineers logs found"
fi
popd
