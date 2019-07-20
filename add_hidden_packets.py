import psycopg2
from datetime import datetime
from time import sleep
import random

hex_digits = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F']
local_digits = ['2', '6', 'A', 'E']

def gen_local_addr():
    addr = ''
    for i in range(1, 18):
        if i % 3 == 0:
            addr += ':'
        elif i == 2:
            addr += local_digits[random.randint(0, 3)]
        else:
            addr += hex_digits[random.randint(0, 15)]
    return addr


try:
    connection = psycopg2.connect(user = "user",
                                  password = "pass",
                                  host = "127.0.0.1",
                                  port = "5432",
                                  database = "pds")

    cursor = connection.cursor()

    # Print PostgreSQL version
    cursor.execute("SELECT version();")
    record = cursor.fetchone()
    print("You are connected to - ", record,"\n")

    # Insert query
    postgres_insert_query = """ INSERT INTO "Record" ("Hash", "MAC", "SSID", "Timestamp", "SequenceCtrl", "X", "Y") VALUES (%s, %s, %s, %s, %s, %s, %s)"""

    # Add packets from 4 different hidden devices (1 record every 10 seconds)
    # in the following 5 minutes (120 packets total).
    # Do not sniff in this time span.

    dev_count = 4
    max_speed = 1
    max_ratio = 1200
    time_span = 10*60
    length = 5
    width = 5
    
    records = []
    records.append([])

    for i in range(1, dev_count+1):
        x = i
        y = i
        direction = 1

        sleep(0.1)
        time = int(datetime.timestamp(datetime.now())*1000)
        stop_time = time + 5*60*1000
        print(time)
        seq_num = i*1000
        
        records.append([])

        while time < stop_time:
            records[i].append(('AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA', gen_local_addr(), '', time, seq_num, x, y))

            # Compute new position
            movement = direction * random.random() * max_speed
            if i%2 == 0:
                x += movement
                if x > length:
                    x = length
                    direction *= -1
                if x < 0:
                    x = 0
                    direction *= -1

            else:
                y += movement
                if y > width:
                    y = width
                    direction *= -1
                if y < 0:
                    y = 0
                    direction *= -1

            # Compute new sequence number
            seq_num = (seq_num + random.randint(0, 1200)) % 4096

            # Compute new time
            time += 10*1000

    for i in range(0, len(records[1])):
        for j in range(1, dev_count+1):
            record_to_insert = records[j][i]
            cursor.execute(postgres_insert_query, record_to_insert)

    connection.commit()
    count = cursor.rowcount
    print (count, "Record inserted successfully into Record table")

except (Exception, psycopg2.Error) as error :
    print ("Error while connecting to PostgreSQL", error)

except (Exception, psycopg2.Error) as error :
    if(connection):
        print("Failed to insert record into mobile table", error)

finally:
    #closing database connection.
        if(connection):
            cursor.close()
            connection.close()
            print("PostgreSQL connection is closed")