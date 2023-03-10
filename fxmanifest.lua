fx_version 'cerulean'
game 'gta5'

files {
    'Client/bin/Release/**/publish/*.dll',
    'config.ini'
}

client_script 'Client/bin/Release/**/publish/*.net.dll'
server_script 'Server/bin/Release/**/publish/*.net.dll'

author 'traditionalism/geneva'
description 'Authentic GTA5:SP vending-machines; remade in FiveM using C#, and Statebags.'