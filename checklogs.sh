pushd "C:\Users\Tatu\AppData\Roaming\SpaceEngineers"
f=$(printf "%s\n" SpaceEngineers_* | sort | tail -n1)
if [ -f "$f" ]; then
    tail -n500 "$f" | grep --color=auto "AHOD" | less
else
    echo "No SpaceEngineers logs found"
fi
popd
