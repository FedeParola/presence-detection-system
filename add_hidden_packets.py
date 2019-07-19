import psycopg2
from datetime import datetime
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

    dev_count = 4
    max_speed = 1
    max_ratio = 1200
    time_span = 10*60

    # for i in range(1, dev_count):
    #     x = i
    #     y = i

    #     time = datetime.timestamp(datetime.now())
    #     print(time)
    #     seq_num = i*1000
        
    #     while time < time + 10*60:
    #         record_to_insert = ('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'AA:AA:AA:AA:AA:AA', 'DEBUG', 120, 100, 0, 0)

    print(gen_local_addr()+'\n')
    print(gen_local_addr()+'\n')
    print(gen_local_addr()+'\n')
    # record_to_insert = ('aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'AA:AA:AA:AA:AA:AA', 'DEBUG', 120, 100, 0, 0)
    # cursor.execute(postgres_insert_query, record_to_insert)
    # connection.commit()
    # count = cursor.rowcount
    # print (count, "Record inserted successfully into Record table")

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