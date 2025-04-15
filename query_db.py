import psycopg
from tabulate import tabulate

# Connect to the database
conn = psycopg.connect("postgres://default:USIBWX1Y4Lui@ep-royal-bird-a1m9xsdr.ap-southeast-1.aws.neon.tech/sep490_backend?sslmode=require")
cursor = conn.cursor()

print("Projects table:")
# First get column names
cursor.execute('SELECT column_name FROM information_schema.columns WHERE table_name = \'Projects\' ORDER BY ordinal_position')
column_names = [row[0] for row in cursor.fetchall()]

# Now get the data
cursor.execute('SELECT * FROM "Projects" LIMIT 10')
rows = cursor.fetchall()
print(tabulate(rows, headers=column_names, tablefmt="grid"))

print("\nProjectUsers table:")
# First get column names
cursor.execute('SELECT column_name FROM information_schema.columns WHERE table_name = \'ProjectUsers\' ORDER BY ordinal_position')
column_names = [row[0] for row in cursor.fetchall()]

# Now get the data
cursor.execute('SELECT * FROM "ProjectUsers" LIMIT 10')
rows = cursor.fetchall()
print(tabulate(rows, headers=column_names, tablefmt="grid"))

# Close the connection
conn.close() 