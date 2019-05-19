import json
import socket

TCP_IP = '127.0.0.1'
TCP_PORT = 13000
BUFFER_SIZE = 1024

file = open('records.json', 'r') 
msg = file.read() 

s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
s.connect((TCP_IP, TCP_PORT))
s.send(msg.encode())
s.shutdown(s.SHUT_WR)
data = s.recv(BUFFER_SIZE)
s.close()
 
print("received data:", data)
