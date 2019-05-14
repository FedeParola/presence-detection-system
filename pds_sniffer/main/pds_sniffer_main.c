#include <stdio.h>
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "freertos/event_groups.h"
#include "esp_system.h"
#include "esp_wifi.h"
#include "esp_event_loop.h"
#include "esp_log.h"
#include "nvs_flash.h"
#include "lwip/err.h"
#include "lwip/sockets.h"
#include "lwip/sys.h"
#include <lwip/netdb.h>
#include <rom/md5_hash.h>
#include <unistd.h>
#include <cJSON.h>
#include <driver/timer.h>
#include <string.h>
#include <time.h>
#include <sys/time.h>

/*debug-terminal commands
	cd ~/esp/presence-detection-system/pds_sniffer
	make flash
	make monitor
*/

/*constants definition*/

//initial configuration parameters
#define CONF_MAX_RETRY_COUNT 10      			//define how many times ESP tries to get the initial configuration
#define CONFIGURATION_PORT 13000				//declaration of the port number on which the initial configuration will arrive
//timer
#define TIMER_TRIGGERED   0						//used in variable "event" -> 0 means that the timer has been triggered
//parameters for the WIFI connection
#define WIFI_SSID  	"NotSoFastBau"
#define WIFI_PASS   "Vivailpolitecnico14!"
//masks for packet sniffing
#define TYPESUBTYPE_MASK 0b0000000011111100
#define TYPE_PROBE 		 0b0000000001000000
//packet handler constant
#define MAX_SSID_LENGTH 256


/*types definition*/

//header frame WiFi IEEE 802.11
typedef struct {
	unsigned frame_ctrl:16;
	unsigned duration_id:16;
	uint8_t addr1[6]; /* receiver address */
	uint8_t addr2[6]; /* sender address */
	uint8_t addr3[6]; /* filtering address */
	unsigned sequence_ctrl:16;
} wifi_ieee80211_mac_hdr_t;

//frame WiFi IEEE 802.11
typedef struct {
	wifi_ieee80211_mac_hdr_t hdr;
	uint8_t payload[]; /* network data ended with 4 bytes csum (CRC32) */
} wifi_ieee80211_packet_t;

//record to store useful data from the sniffed packets
typedef struct {
	char SSID[MAX_SSID_LENGTH];
	char MACADDR[18];
	int RSSI;
	char hash[33];
	uint64_t timestamp;
} record_t;

/*prototypes definition*/

void set_date(time_t timestampToSet);  								//sets the system date with the value of the receved timestamp
int recv_configuration();											//makes the first configuration of the ESP from the Desktop app
int parse_configuration(char *buffer);								//parse the configuration message and apply it
void connect_wifi();												//prepare the wifi connection
static esp_err_t event_handler(void *ctx, system_event_t *event);	//manage wifi events, if no problems it connect to the wifi
void reboot_task();													//create a background task waiting for reboot command (sended by the desktop app)
void create_timer();												//create timer
static void timer_callback(void* arg);  							//procedure called every time the timer is triggered -> sets the event variable to the TIMER_TRIGGERED value (0)
static void timer_task(void *arg);									//stops the sniffer task -> open and send on a socket the JSON array -> restart timer and sniffer
int create_ipv4_listen_socket(char reason);									//create a listening socket
int create_ipv4_socket_client();									//create a client socket connection
void set_packets_sniffer();											//starts to sniffing packets -> call the packet_handler at every received packet
void unset_packets_sniffer();										//stops the sniffer
void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type);	//function called every time a packet has been sniffed
char *macaddr_to_str(const uint8_t macaddr[6], char str[18]);		//convert a MAC ADDRESS into string
void md5(unsigned char *data, int dataLen, unsigned char *hash);	//calculate md5 hash of a packet
char *hash_to_str(const unsigned char hash[16], char str[33]);		//converts the md5 hash into string so that we can store it
void add_json_record(record_t r);									//create a single JSON record of the received packet

/*global variables definition*/

/* Server connection parameters */
char server_ip[16];
char server_port[6];

//timer declarations
esp_timer_handle_t timer;
xQueueHandle timer_queue;
int timer_countdown;		//duration of the timer
// Event group
static EventGroupHandle_t wifi_event_group;
const int CONNECTED_BIT = BIT0;
//channel declaration
int channel;				//channel to sniff packets on
//flag to set the promiscus mode
int firstSet=1;
//record declaration
record_t record;
//socket
int sock;
//JSON
cJSON *root,*data; 			//the single JSON

//Main application
void app_main() {

	//disable the default wifi logging
	esp_log_level_set("wifi", ESP_LOG_NONE);

	//connect to the wifi
	connect_wifi();

	/* Starts to listen a TCP socket in order to receive the initial configuration */
	if (recv_configuration() != 0) {
		ESP_LOGE("MAIN", "Initial configuration problem... Restarting!");
		esp_restart();
	}

	//create a background task waiting for reboot command (sended by the desktop app in async)
	xTaskCreate(reboot_task, "ESP_reboot_task", 2048, NULL, 1, NULL);

	//JSON array creation
	data = cJSON_CreateArray();
	//timer creation
	create_timer();
	//start to sniffing packets
	set_packets_sniffer();

}

void set_date(time_t timestampToSet){
	    struct timeval now = { .tv_sec = timestampToSet };
	    settimeofday(&now, NULL);
}

int recv_configuration() {
	char *tag = "CONF";	// Tag for logging purposes

	int received;	// Tells if the configuration was correctly received
	int error; 		// Tells if there were errors during the reception or parsing of the configuration
	int count;		// N of attempts to receive a configuration

	int nrecv;				// Bytes received in a single recv
	int msglen;				// Total size of the received message
	char rx_buffer[128];
	char ack = 'A';

	int listen_sock;
	struct sockaddr_in remote_addr;
	size_t addrlen;
	char addr_str[128];

	/* Create a listening socket */
	listen_sock = create_ipv4_listen_socket('C');
	if(listen_sock == -1) {
		return -1;
	}

	ESP_LOGI(tag, "Socket listening...");

	/* Receive incoming connections */
	received = 0;
	count = 0;

	while (!received && count < CONF_MAX_RETRY_COUNT) {
		count++;

		/* Accept an incoming connection */
		addrlen = sizeof(remote_addr);
		int sock = accept(listen_sock, (struct sockaddr *) &remote_addr, &addrlen);
		if (sock < 0) {
			ESP_LOGE(tag, "Unable to accept connection: errno %d", errno);

		} else {
			/* Print remote host information */
			inet_ntoa_r(((struct sockaddr_in *) &remote_addr)->sin_addr.s_addr, addr_str, sizeof(addr_str)-1);
			ESP_LOGI(tag, "Accepted connection from %s", addr_str);

			msglen = 0;
			error = 0;
			while (!received && !error) {
				nrecv = recv(sock, rx_buffer+msglen, sizeof(rx_buffer)-msglen-1, 0);

				/* Error receiving data */
				if (nrecv < 0) {
					ESP_LOGE(tag, "recv failed: errno %d", errno);
					error = 1;

				/* Connection closed before message terminated */
				} else if (nrecv == 0) {
					ESP_LOGE(tag, "Connection closed prematurely");
					error = 1;

				/* Data received */
				} else {
					msglen += nrecv;

					/* Check if message is terminated */
					if (rx_buffer[msglen-1] == 0) {
						rx_buffer[msglen] = '\0';
						ESP_LOGI(tag, "Received message:\n%s", rx_buffer);

						/* Try to parse and apply the configuration */
						if(parse_configuration(rx_buffer) == 0) {

							/* Send ACK */
							if (send(sock, &ack, sizeof(ack), 0) != sizeof(ack)) {
								ESP_LOGE(tag, "Error sending the acknowledgement");
								error = 1;

							} else {
								received = 1;
							}

						} else {
							error = 1;
						}
					}
				}
			}

			/* Close the connected socket */
			close(sock);
		}
	}

	if(count >= CONF_MAX_RETRY_COUNT){
		ESP_LOGE(tag, "Exceeded max number of configuration attempts");
	}

	/* Close the listening socket */
	ESP_LOGI(tag, "Shutting down socket");
	close(listen_sock);


	if (received) {
		return 0;
	} else {
		return -1;
	}
}

int parse_configuration(char *buffer) {
	char *tag = "CONF";	// Tag for logging purposes

	cJSON *conf_json, *field;
	uint64_t timestamp;

	if (buffer == NULL) {
		return -1;
	}

	/* Try to parse the configuration message */
	conf_json = cJSON_Parse(buffer);
	if(!cJSON_IsObject(conf_json)) {
		ESP_LOGE(tag, "Error parsing the configuration json");
		return -1;
	}

	/* Retrieve timestamp */
	field = cJSON_GetObjectItem(conf_json, "timestamp");
	if(!cJSON_IsNumber(field)) {
		ESP_LOGE(tag, "Error parsing timestamp field");
		return -1;
	}
	timestamp = (uint64_t) field->valuedouble;
	set_date(timestamp);

	/* Retrieve server address */
	field = cJSON_GetObjectItem(conf_json, "ipAddress");
	if(!cJSON_IsString(field)) {
		ESP_LOGE(tag, "Error parsing ipAddress field");
		return -1;
	}
	strncpy(server_ip, field->valuestring, sizeof(server_ip));

	/* Retrieve server port */
	field = cJSON_GetObjectItem(conf_json, "port");
	if(!cJSON_IsString(field)) {
		ESP_LOGE(tag, "Error parsing port field");
		return -1;
	}
	strncpy(server_port, field->valuestring, sizeof(server_port));

	/* Retrieve sniffing channel */
	field = cJSON_GetObjectItem(conf_json, "channel");
	if(!cJSON_IsNumber(field)) {
		ESP_LOGE(tag, "Error parsing channel field");
		return -1;
	}

	channel = field->valueint;
	//sets the channel to sniff packets on
	ESP_ERROR_CHECK(esp_wifi_set_channel(channel, WIFI_SECOND_CHAN_NONE));

	/* Retrieve timer_countdown */
	field = cJSON_GetObjectItem(conf_json, "timer_count");
	if(!cJSON_IsNumber(field)){
		ESP_LOGE(tag, "Error parting timer_count field");
		return -1;
	}
	timer_countdown = 1000000*field->valueint;

	cJSON_Delete(conf_json);

	ESP_LOGI(tag, "Configuration correctly applied:\n"
			"timestamp = %llu\n"
			"ip address = %s\n"
			"port = %s\n"
			"channel = %u\n"
			"timer_count = %u", timestamp, server_ip, server_port, channel, timer_countdown);

	return 0;
}

void connect_wifi(){
		// initialize NVS
		ESP_ERROR_CHECK(nvs_flash_init());

		// create the event group to handle wifi events
		wifi_event_group = xEventGroupCreate();

		// initialize the tcp stack
		tcpip_adapter_init();

		// initialize the wifi event handler
		ESP_ERROR_CHECK(esp_event_loop_init(event_handler, NULL));

		// initialize the wifi stack in STAtion mode with config in RAM
		wifi_init_config_t wifi_init_config = WIFI_INIT_CONFIG_DEFAULT();
		ESP_ERROR_CHECK(esp_wifi_init(&wifi_init_config));
		ESP_ERROR_CHECK(esp_wifi_set_storage(WIFI_STORAGE_RAM));
		ESP_ERROR_CHECK(esp_wifi_set_mode(WIFI_MODE_STA));

		// configure the wifi connection and start the interface
		wifi_config_t wifi_config = {
			   .sta = {
				   .ssid = WIFI_SSID,
				   .password = WIFI_PASS,
			   },
		   };

		ESP_ERROR_CHECK(esp_wifi_set_config(ESP_IF_WIFI_STA, &wifi_config));
	    ESP_ERROR_CHECK(esp_wifi_start());
		printf("Connecting to %s\n", WIFI_SSID);

		// wait for connection
		printf("Main task: waiting for connection to the wifi network...\n ");
		xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);

		printf("connected!\n");

		// print the local IP address
		tcpip_adapter_ip_info_t ip_info;
		ESP_ERROR_CHECK(tcpip_adapter_get_ip_info(TCPIP_ADAPTER_IF_STA, &ip_info));

		/* DEBUG: check channel */
		uint8_t channel;
		wifi_second_chan_t second;
		ESP_ERROR_CHECK( esp_wifi_get_channel(&channel, &second) );
		ESP_LOGI("CONN", "Channel: %u", channel);

}

static esp_err_t event_handler(void *ctx, system_event_t *event){

    switch(event->event_id) {
		case SYSTEM_EVENT_STA_START:
			esp_wifi_connect();
			break;
		//used for dhcp, using static ip addresses it could be eliminated
		case SYSTEM_EVENT_STA_GOT_IP:
			xEventGroupSetBits(wifi_event_group, CONNECTED_BIT);
			break;

		case SYSTEM_EVENT_STA_DISCONNECTED:
			xEventGroupClearBits(wifi_event_group, CONNECTED_BIT);
			break;

		default:
			break;
    }

	return ESP_OK;
}

void reboot_task(){
	char *tag = "REBOOT";	// Tag for logging purposes

	int received;	// Tells if the command is correctly received
	int nrecv;		// Bytes received in a single recv
	char rx_char;

	int listen_sock;
	struct sockaddr_in remote_addr;
	size_t addrlen;
	char addr_str[128];

	//infinite cycle to manage the rebooting recv errors
	while(1){
		/* Create a listening socket */
		listen_sock = create_ipv4_listen_socket('R');
		if(listen_sock == -1) {
			ESP_LOGE("REBOOT", "Reboot task problem... Restarting!");
			esp_restart();
		}

		ESP_LOGI(tag, "Socket listening...");

		/* Receive incoming connections */
		received = 0;

		while (!received) {
			/* Accept an incoming connection */
			addrlen = sizeof(remote_addr);
			int sock = accept(listen_sock, (struct sockaddr *) &remote_addr, &addrlen);
			if (sock < 0) {
				ESP_LOGE(tag, "Unable to accept connection: errno %d", errno);
				/* Close the listening socket */
				ESP_LOGI(tag, "Shutting down socket");
				close(sock);
				close(listen_sock);
				break; //stop the cycle and waits for a new connection
			} else {
				/* Print remote host information */
				inet_ntoa_r(((struct sockaddr_in *) &remote_addr)->sin_addr.s_addr, addr_str, sizeof(addr_str)-1);
				ESP_LOGI(tag, "Accepted connection from %s", addr_str);
				nrecv = recv(sock, &rx_char, sizeof(rx_char), 0);
				/* Error receiving data */
				if (nrecv < 0) {
					ESP_LOGE(tag, "nrecv: %d", nrecv);
					ESP_LOGE(tag, "recv failed: errno %d", errno);
					/* Close the listening socket */
					ESP_LOGI(tag, "Shutting down socket");
					close(sock);
					close(listen_sock);
					break; //stop the cycle and waits for a new connection
				/* Connection closed before message terminated */
				} else if (nrecv == 0) {
					ESP_LOGE(tag, "Connection closed prematurely");
					/* Close the listening socket */
					ESP_LOGI(tag, "Shutting down socket");
					close(sock);
					close(listen_sock);
					break; //stop the cycle and waits for a new connection
				/* Data received */
				} else {
					/* Check if message is terminated */
					ESP_LOGI(tag, "Received message: %c", rx_char);

					/* Try to parse and apply the configuration */
					if(rx_char == 'R') {
							received=1;
					}else{
						/* Close the listening socket */
						ESP_LOGI(tag, "Shutting down socket");
						close(sock);
						close(listen_sock);
						break; //stop the cycle and waits for a new connection
					}

					/* Close the connected socket */
					close(sock);
				}
			}

			/* Close the listening socket */
			ESP_LOGI(tag, "Shutting down socket");
			close(listen_sock);

			ESP_LOGI(tag, "Rebooting...");
			esp_restart();
		}
	}
}

void create_timer(){
	//create a queue of parameters the will be passed from the callback to the actual task
	timer_queue = xQueueCreate(10, sizeof(int));

	/* Create a one-shot timer which will fire after 10s */
	const esp_timer_create_args_t timer_args = {
			.callback = &timer_callback,
			.name = "send-timer"
	};
	ESP_ERROR_CHECK( esp_timer_create(&timer_args, &timer) );

	/* Start the timer */
	ESP_ERROR_CHECK( esp_timer_start_once(timer, timer_countdown) );

	//start the procedure tha will be listening for events putted in the queue by the callback (one every trigger of the timer)
	xTaskCreate(timer_task, "timer_evt_task", 4096, NULL, 5, NULL);
}

static void timer_callback(void* arg) {
	ESP_LOGI("TIMER", "Timer called, current val: %lld us", esp_timer_get_time());

	int event = TIMER_TRIGGERED;

	/* Now just send the event data back to the main program task */
	xQueueSendFromISR(timer_queue, &event, NULL);
}

static void timer_task(void *arg){
	int event;
	char recv_buf[100]; 	//buffer to save response data coming from the desktop app
	int r;					//number of chars read by the read funtion at every try
	int readChars=0;		//received string length, how mane chars putted in recv_buf, number of total read chars
	cJSON *serverResponse;	//JSON received from the desktop app
	char *data_json;		//string of JSON array

	while(1){
		xQueueReceive(timer_queue, &event, portMAX_DELAY);
		if (event == TIMER_TRIGGERED) {
				//packet sending

				//stop the sniffer
				unset_packets_sniffer();

				//create a socket
				sock=create_ipv4_socket_client();

				//convert JSON in a string
				data_json = cJSON_Print(data);
				//send the data
				if(write(sock, data_json, strlen(data_json))==strlen(data_json)){
					ets_printf("packets has been send\n");
				}
				//destroy the created string
				free(data_json);

				//response receive from the desktop app
				memset(recv_buf, 0, sizeof(recv_buf));
				readChars=0;
				do {
				  r = read(sock, recv_buf + readChars, sizeof(recv_buf) - readChars - 1);
				  readChars+=r;
				} while(r > 0);
				recv_buf[readChars]='\0';

				//pass the read string to a JSON object
				serverResponse=cJSON_Parse(recv_buf);

				//close the socket
				close(sock);
				ets_printf("socket has been closed\n");

				//set the new system date with the value received from the desktop app
				set_date(cJSON_GetObjectItem(serverResponse,"timestamp")->valueint);

				//destroy the JSON array
				cJSON_Delete(data);

				//create a new JSON array
				data = cJSON_CreateArray();

				//restart the sniffer
				set_packets_sniffer();

				//Restart the timer
				ESP_ERROR_CHECK( esp_timer_start_once(timer, timer_countdown) );
				ESP_LOGI("TIMER-TASK", "Timer restarted, current val: %lld us", esp_timer_get_time());
		}
	}
}

int create_ipv4_listen_socket(char reason) {
	char *tag; // Tag for logging purposes

	if(reason == 'R'){
		tag = "REBOOT";
	}else{
		tag = "CONF";
	}


	int sock;
	struct sockaddr_in local_addr;

	local_addr.sin_addr.s_addr = htonl(INADDR_ANY);
	local_addr.sin_family = AF_INET;
	local_addr.sin_port = htons(CONFIGURATION_PORT);

	sock = socket(AF_INET, SOCK_STREAM, IPPROTO_IP);
	if (sock < 0) {
		ESP_LOGE(tag, "Unable to create socket: errno %d", errno);
		return -1;
	}

	/*
	 * used to bind again the same socket
	 * without this piece of code the binding returns errno = 112 when trying to bind the socket for reboot
	 */
	int flag = 1;
	setsockopt(sock, SOL_SOCKET, SO_REUSEADDR, &flag, sizeof(flag));

	if (bind(sock, (struct sockaddr *) &local_addr, sizeof(local_addr)) != 0) {
		ESP_LOGE(tag, "Socket unable to bind: errno %d", errno);
		close(sock);
		return -1;
	}

	if (listen(sock, 1) != 0) {
		ESP_LOGE(tag, "Error occurred during listen: errno %d", errno);
		close(sock);
		return -1;
	}

	return sock;
}

int create_ipv4_socket_client(){
  struct addrinfo hints;
  struct addrinfo *res;
  struct in_addr *addr;

  hints.ai_family = AF_INET;
  hints.ai_socktype = SOCK_STREAM;

  int err = getaddrinfo(server_ip, server_port, &hints, &res);

  if(err != 0 || res == NULL) {
    printf("DNS lookup failed err=%d res=%p\n", err, res);
    return -1;
  }

  /* Code to print the resolved IP.

     Note: inet_ntoa is non-reentrant, look at ipaddr_ntoa_r for "real" code */
  addr = &((struct sockaddr_in *)res->ai_addr)->sin_addr;
  printf("DNS lookup succeeded. IP=%s\n", inet_ntoa(*addr));

  int l_sock = socket(res->ai_family, res->ai_socktype, 0);
  if(l_sock < 0) {
    printf("... Failed to allocate socket.\n");
    freeaddrinfo(res);
    return -1;
  }

  struct timeval to;
  to.tv_sec = 2;
  to.tv_usec = 0;
  setsockopt(l_sock,SOL_SOCKET,SO_SNDTIMEO,&to,sizeof(to));

  if(connect(l_sock, res->ai_addr, res->ai_addrlen) != 0) {
    printf("... socket connect failed errno=%d\n", errno);
    close(l_sock);
    freeaddrinfo(res);
    return -1;
  }

  printf("... connected\n");
  freeaddrinfo(res);

  // All set, socket is configured for sending and receiving
  return l_sock;
}

void set_packets_sniffer(){
	if(firstSet){
		wifi_promiscuous_filter_t filter = {.filter_mask=WIFI_PROMIS_FILTER_MASK_MGMT};
		/* enable promiscuous mode and set packet handler */
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous(true));
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous_filter(&filter));

		firstSet=0;
	}
	//sets the function that will be called at every received packet
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(&packet_handler));
}

void unset_packets_sniffer(){
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(NULL));
}

void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type){
	const wifi_promiscuous_pkt_t *ppkt = (wifi_promiscuous_pkt_t *) buf;
	const wifi_ieee80211_packet_t *ipkt = (wifi_ieee80211_packet_t *) ppkt->payload;
	const wifi_ieee80211_mac_hdr_t *hdr = &ipkt->hdr;
	char macaddr_str[18], ssid_str[MAX_SSID_LENGTH] = "";
	int ssid_length = 0;
	struct timeval now;

	//temp hash
	unsigned char hashtmp[16];

	/* check if it is a probe request */
	if(((hdr->frame_ctrl & TYPESUBTYPE_MASK) ^ TYPE_PROBE) == 0){
		// if channel is different from the channel of the connection to the router
		// skip packets that may be sniffed of the connection to the router channel
		if(ppkt->rx_ctrl.channel != channel){
			ESP_LOGI("Sniffer", "Skipped packet on channel %d", ppkt->rx_ctrl.channel);
			return;
		}

		/* look for SSID in the payload */
		if(ipkt->payload[0] == 0){ // ssid field is present
			ssid_length = ipkt->payload[1];
			for(int i = 0; i < ssid_length; i++) ssid_str[i] = ipkt->payload[2+i];
			ssid_str[ssid_length] = '\0';

		}

		//takes the system date again to mark the packets
		gettimeofday(&now, NULL);

		//save data in the record
		macaddr_to_str(hdr->addr2, record.MACADDR);
		memcpy(record.SSID, ssid_str, ssid_length+1);
		record.RSSI = ppkt->rx_ctrl.rssi;
		//make hash md5 of the packet content
		md5((unsigned char *) ipkt, sizeof(*ipkt), hashtmp);
		hash_to_str(hashtmp, record.hash);
		record.timestamp = ((uint64_t) now.tv_sec)*1000 + now.tv_usec/1000;

		//add to JSON the sniffed packet
		add_json_record(record);

		//dispay of the monitor the sniffer packet
		ets_printf("TIMESTAMP s:%u ms:%u CHAN=%02d, SEQ=%4x, RSSI=%d, ADDR=%s, SSID='%s'\n",
				now.tv_sec,
				now.tv_usec/1000,
				ppkt->rx_ctrl.channel,
				hdr->sequence_ctrl,
				ppkt->rx_ctrl.rssi,
				macaddr_to_str(hdr->addr2, macaddr_str),
				ssid_str);

	}
}

char *macaddr_to_str(const uint8_t macaddr[6], char str[18]){

	sprintf(str, "%02X:%02X:%02X:%02X:%02X:%02X",
			(unsigned) macaddr[0],
			(unsigned) macaddr[1],
			(unsigned) macaddr[2],
			(unsigned) macaddr[3],
			(unsigned) macaddr[4],
			(unsigned) macaddr[5]);

	return str;
}

void md5(unsigned char *data, int dataLen, unsigned char *hash){
	struct MD5Context myContext;

	memset(&myContext,0x00,sizeof(myContext));
	memset(hash,0x00,16);

	MD5Init(&myContext);
	MD5Update(&myContext, data, dataLen);
	MD5Final(hash, &myContext);

	hash[17]='\0';
}

char *hash_to_str(const unsigned char hash[16], char str[33]){

	str[0]='\0';

	for(int i=0; i<16; i++){
		sprintf(str, "%s%02X", str, hash[i]);
	}

	return str;
}

void add_json_record(record_t r){
    root = cJSON_CreateObject();

    cJSON_AddItemToObject(root, "SSID", cJSON_CreateString(r.SSID));
    cJSON_AddItemToObject(root, "MACADDR", cJSON_CreateString(r.MACADDR));
    cJSON_AddNumberToObject(root, "RSSI", r.RSSI);
    cJSON_AddItemToObject(root, "hash", cJSON_CreateString(r.hash));
    cJSON_AddNumberToObject(root, "timestamp", r.timestamp);
    cJSON_AddItemToArray(data, root);

    return;
}
