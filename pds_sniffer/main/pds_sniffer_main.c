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

/*per il debug*/

//cd ~/esp/hello_world
//make flash
//make monitor

/*define constants*/

#define CONF_MAX_RETRY_COUNT 10

#define TIMER_COUNTDOWN 10*1000000
//0 significa timer scattato
#define TIMER_TRIGGERED   0
//dichiaro la porta su cui voglio ricevere la configurazione
#define CONFIGURATION_PORT 13000
//parametri di connessione al WIFI
#define WIFI_SSID "NotSoFastBau"
#define WIFI_PASS "Vivailpolitecnico14!"
//parametri della creazione socket
#define SERVER_PORT "13000"
//maschere per poter sniffare i pacchetti
#define TYPESUBTYPE_MASK 0b0000000011111100
#define TYPE_PROBE 		 0b0000000001000000
//costante usata nella packet handler
#define MAX_SSID_LENGTH 256

/*type definitions*/

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

//record contenete i campi di interesse dei pacchetti
typedef struct {
	char SSID[MAX_SSID_LENGTH];
	char MACADDR[18];
	int RSSI;
	char hash[33];
	uint64_t timestamp;
} record_t;

/*prototipi funzioni*/

void impostaData(time_t timestampToSet);  							//imposta la data di sistema con il timestamp ricevuto
int recv_configuration();											//aspettiamo una configurazione della schedina dal server
int parse_configuration(char *buffer);								// Parse the configuration message and apply it
void connectWIFI();													//prepara tutto per la connessione WIFI
static esp_err_t event_handler(void *ctx, system_event_t *event);	//gestisce gli eventi del WIFI e se va tutto bene si collega
void create_timer();												//creazione del timer
static void timer_callback(void* arg);  							//funzione attivata quando scatta il timer -> setta la variabile evento a 0
static void timer_task(void *arg);									//ferma lo sniffer -> apre e invia sul socket l'array JSON -> fa ripartire il timer e lo sniffing
int create_ipv4_listen_socket();									// Create a listen socket
int create_ipv4_socket_client();									//funzione che crea la connessione socket come client
void sniffaPacchetti();												//iniziamo a sniffare instanziando un packet_handler ad ogni pacchetto ricevuto
void unsetSniffaPacchetti();										//ferma lo sniffer --> gestire ancora bene il reset
void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type);	//funzione richiamata ogni volta che viene ricevuto un pacchetto
char *macaddr_to_str(const uint8_t macaddr[6], char str[18]);		//converte il MAC ADDRESS in stringa
void md5(unsigned char *data, int dataLen, unsigned char *hash);	//calcola hash md5 nel pacchetto
char *hash_to_str(const unsigned char hash[16], char str[33]);		//funzione che converte il hash md5 in stringa in modo da poterlo salvare nel record
void add_json_record(record_t r);									//crea il singolo JSON relativo al pacchetto ricevuto

/*global variables definition*/

/* Server connection parameters */
char server_ip[16];
char server_port[6];

//dichiarazione del timer
esp_timer_handle_t timer;
xQueueHandle timer_queue;
// Event group
static EventGroupHandle_t wifi_event_group;
const int CONNECTED_BIT = BIT0;
//flag per l'impostazione della modalita promiscus
int firstSet=1;
//dichiariamo il record
record_t record;
//socket
int sock;
//JSON
cJSON *root,*data; 		//singolo JSON
//int sniffedPackets=0; 	//numero di pacchetti sniffati mandati al termine della sessione di sniffing

// Main application
void app_main() {

	//disable the default wifi logging
	esp_log_level_set("wifi", ESP_LOG_NONE);

	//connessione al wifi
	connectWIFI();

	/* Ci mettiamo in ascolto sul socket TCP per ricevere una configurazione iniziale */
	if (recv_configuration() != 0) {
		ESP_LOGE("MAIN", "Initial configuration problem... Restarting!");
		esp_restart();
	}

	//creiamo l'array di JSON
	data = cJSON_CreateArray();
	//creazione del timer
	create_timer();
	//sniffiamo qualcosa
	sniffaPacchetti();

}

void impostaData(time_t timestampToSet){
	    struct timeval now = { .tv_sec = timestampToSet };
	    settimeofday(&now, NULL);
}

int create_ipv4_listen_socket() {
	char *tag = "CONF";	// Tag for logging purposes

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

int parse_configuration(char *buffer) {
	char *tag = "CONF";	// Tag for logging purposes

	cJSON *conf_json, *field;
	uint64_t timestamp;
	int channel;

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
	impostaData(timestamp);

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
	esp_wifi_set_channel(channel, WIFI_SECOND_CHAN_NONE);

	cJSON_Delete(conf_json);

	ESP_LOGI(tag, "Configuration correctly applied:\n"
			"timestamp = %llu\n"
			"ip address = %s\n"
			"port = %s\n"
			"channel = %u", timestamp, server_ip, server_port, channel);

	return 0;
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
	listen_sock = create_ipv4_listen_socket();
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

void connectWIFI(){
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
		printf("Main task: waiting for connection to the wifi network... ");
		xEventGroupWaitBits(wifi_event_group, CONNECTED_BIT, false, true, portMAX_DELAY);

		printf("connected!\n");

		// print the local IP address
		tcpip_adapter_ip_info_t ip_info;
		ESP_ERROR_CHECK(tcpip_adapter_get_ip_info(TCPIP_ADAPTER_IF_STA, &ip_info));
		printf("IP Address:  %s\n", ip4addr_ntoa(&ip_info.ip));
		printf("Subnet mask: %s\n", ip4addr_ntoa(&ip_info.netmask));
		printf("Gateway:     %s\n", ip4addr_ntoa(&ip_info.gw));

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

		case SYSTEM_EVENT_STA_GOT_IP://serve per gestire il dhcp, se usiamo gli indirizzi ip statici di può eliminare
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

void create_timer(){
	//crea una coda di parametri che verranno passati dalla callback al task effettivo
	timer_queue = xQueueCreate(10, sizeof(int));

	/* Create a one-shot timer which will fire after 10s */
	const esp_timer_create_args_t timer_args = {
			.callback = &timer_callback,
			.name = "send-timer"
	};
	ESP_ERROR_CHECK( esp_timer_create(&timer_args, &timer) );

	/* Start the timer */
	ESP_ERROR_CHECK( esp_timer_start_once(timer, TIMER_COUNTDOWN) );

	//fa partire la funzione che sarà in ascolto di eventi messi in coda dalla callback ad ogni scatto
	xTaskCreate(timer_task, "timer_evt_task", 4096, NULL, 5, NULL);
}

static void timer_callback(void* arg) {
	ESP_LOGI("TIMER", "Timer called, current val: %lld us", esp_timer_get_time());

	int evento = TIMER_TRIGGERED;

	/* Now just send the event data back to the main program task */
	xQueueSendFromISR(timer_queue, &evento, NULL);
}

static void timer_task(void *arg){
	int evento;
	char recv_buf[100]; 	//buffer in cui finiranno i dati letti in risposta dal server
	int r;					//numeri di caratteri letti dalla read ad ogni botta
	int readChars=0;		//lunghezza della stringa ricevuta, ovvero quanta roba ho messo nel recv_buf, ovvero quanti caratteri ho letto
	cJSON *serverResponse;	//JSON di risposta del server
	char *data_json;		//stringa di JSON array

	while(1){
		xQueueReceive(timer_queue, &evento, portMAX_DELAY);
		if (evento == TIMER_TRIGGERED) {
				//pezzo con la gestione dell'invio dei pacchetti

				//fermiamo lo sniffer
				unsetSniffaPacchetti();

				//creazione del socket
				sock=create_ipv4_socket_client();

				//convertiamo il JSON in stringa
				data_json = cJSON_Print(data);
				//mandiamo i dati
				if(write(sock, data_json, strlen(data_json))==strlen(data_json)){
					ets_printf("pacchetti mandati\n");
				}
				//distruggiamo subito la stringa creata
				free(data_json);

				//ricevo risposta dal server
				memset(recv_buf, 0, sizeof(recv_buf));
				readChars=0;
				do {
				  r = read(sock, recv_buf + readChars, sizeof(recv_buf) - readChars - 1);
				  readChars+=r;
				} while(r > 0);
				recv_buf[readChars]='\0';

				//passo la stringa letta ad un ogetto JSON
				serverResponse=cJSON_Parse(recv_buf);

				//chiudiamo il socket
				close(sock);
				ets_printf("socket chiuso\n");

				//imposto la dato di sistema con il nuovo valore ricevuto in risposta dal server
				impostaData(cJSON_GetObjectItem(serverResponse,"timestamp")->valueint);

				//distruggiamo l'array JSON appena mandato
				cJSON_Delete(data);

				//creiamo un nuovo array JSON
				data = cJSON_CreateArray();

				//facciamo ripartire lo sniffer
				sniffaPacchetti();

				//Restart the timer
				ESP_ERROR_CHECK( esp_timer_start_once(timer, TIMER_COUNTDOWN) );
				ESP_LOGI("TIMER-TASK", "Timer restarted, current val: %lld us", esp_timer_get_time());
		}
	}
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

void sniffaPacchetti(){
	if(firstSet){
		wifi_promiscuous_filter_t filter = {.filter_mask=WIFI_PROMIS_FILTER_MASK_MGMT};
		/* enable promiscuous mode and set packet handler */
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous(true));
		ESP_ERROR_CHECK(esp_wifi_set_promiscuous_filter(&filter));

		firstSet=0;
	}
	//impostiamo la funzione che verrà chiamata ad ogni pacchetto ricevuto
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(&packet_handler));
}

void unsetSniffaPacchetti(){
	ESP_ERROR_CHECK(esp_wifi_set_promiscuous_rx_cb(NULL));
}

void packet_handler(void *buf, wifi_promiscuous_pkt_type_t type){
	const wifi_promiscuous_pkt_t *ppkt = (wifi_promiscuous_pkt_t *) buf;
	const wifi_ieee80211_packet_t *ipkt = (wifi_ieee80211_packet_t *) ppkt->payload;
	const wifi_ieee80211_mac_hdr_t *hdr = &ipkt->hdr;
	char macaddr_str[18], ssid_str[MAX_SSID_LENGTH] = "";
	int ssid_length = 0;
	struct timeval now;

	//hash temporaneo
	unsigned char hashtmp[16];

	/* check if it is a probe request */
	if(((hdr->frame_ctrl & TYPESUBTYPE_MASK) ^ TYPE_PROBE) == 0){

		/* look for SSID in the payload */
		if(ipkt->payload[0] == 0){ // ssid field is present
			ssid_length = ipkt->payload[1];
			for(int i = 0; i < ssid_length; i++) ssid_str[i] = ipkt->payload[2+i];
			ssid_str[ssid_length] = '\0';

		}

		//Prende la data di sistema di nuovo per marcare i pacchetti
		gettimeofday(&now, NULL);

		//salvo nel record i dati scritti
		macaddr_to_str(hdr->addr2, record.MACADDR);
		memcpy(record.SSID, ssid_str, ssid_length+1);
		record.RSSI = ppkt->rx_ctrl.rssi;
		//facciamo md5 del contenuto del pacchetto
		md5((unsigned char *) ipkt, sizeof(*ipkt), hashtmp);
		hash_to_str(hashtmp, record.hash);
		//record.timestamp=ppkt->rx_ctrl.timestamp;
		record.timestamp = ((uint64_t) now.tv_sec)*1000 + now.tv_usec/1000;

		//aggiungiamo nel JSON il pacchetto sniffato
		add_json_record(record);

		//stampiamo i pacchetti sniffati
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
