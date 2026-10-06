#!/bin/bash

## Install additional apt packages
sudo apt-get update && \
    # sudo apt upgrade -y && \
    sudo apt-get install -y --no-install-recommends dos2unix libsecret-1-0 xdg-utils && \
    sudo apt-get clean -y && \
    sudo rm -rf /var/lib/apt/lists/*

## Configure git
echo Configure git
git config --global pull.rebase false
git config --global core.autocrlf input

# D2Coding Nerd Font
# echo Install D2Coding Nerd Font
# mkdir -p $HOME/.local
# mkdir -p $HOME/.local/share
# mkdir -p $HOME/.local/share/fonts
# wget https://github.com/ryanoasis/nerd-fonts/releases/latest/download/D2Coding.zip
# unzip D2Coding.zip -d $HOME/.local/share/fonts
# rm D2Coding.zip

## OH-MY-POSH ##
echo Install oh-my-posh
sudo wget https://github.com/JanDeDobbeleer/oh-my-posh/releases/latest/download/posh-linux-amd64 -O /usr/local/bin/oh-my-posh
sudo chmod +x /usr/local/bin/oh-my-posh

echo DONE!
