#!/bin/bash
versionNumber=$(grep '<Version>' ./PxApi/PxApi.csproj | grep -o "[0-9]*\.[0-9]*\.[0-9]*")

git fetch origin dev --quiet

versionInDev=$(git show origin/dev:PxApi/PxApi.csproj | grep '<Version>' | grep -o "[0-9]*\.[0-9]*\.[0-9]*")

echo "Version: This branch $versionNumber, dev branch $versionInDev"

smallerOrEqual() {
	if [[ $1 == $2 ]]
	then
		return 0
	fi

	local oldIFS=$IFS
	IFS='.'
	read -ra in1Vers <<< "$1"
	read -ra in2Vers <<< "$2"
	IFS=$oldIFS

	for i in 0 1 2
	do
		if ((10#${in1Vers[i]} > 10#${in2Vers[i]}))
		then
			return 1
		fi
	done
	return 0
}

if ! git diff --quiet origin/dev HEAD PxApi; then
	if smallerOrEqual $versionNumber $versionInDev
	then
		echo "Version number needs to be updated."
		exit 1
	fi
fi