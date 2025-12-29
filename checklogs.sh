pushd "C:\Users\Tatu\AppData\Roaming\SpaceEngineers"
outp="E:\gitrepos\AHOD\lastlog.log"
f=$(printf "%s\n" SpaceEngineers_* | sort | tail -n1)
if [ -f "$f" ]; then
    tail -n500 "$f" | grep --color=auto "AHOD" > "$outp"
	cat "$outp"
else
    echo "No SpaceEngineers logs found"
fi
popd
