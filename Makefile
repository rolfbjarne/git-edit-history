all:
	xbuild

install: $(HOME)/bin/git-edit-history all

$(HOME)/bin/git-edit-history: git-edit-history Makefile
	cat git-edit-history | sed 's_%DIR%_$(abspath $(CURDIR))/bin/Debug/_' > ~/bin/git-edit-history
	chmod +x $@
