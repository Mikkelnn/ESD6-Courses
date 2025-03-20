library IEEE;
use IEEE.STD_LOGIC_1164.ALL;
use IEEE.STD_LOGIC_ARITH.ALL;
use IEEE.STD_LOGIC_UNSIGNED.ALL;

entity MatrixMultiplier_FPGA is
    generic (N : integer := 4);
    port (
        clk    : in std_logic;  -- 12 MHz system clock
        btn    : in std_logic;  -- Button for start signal
        sw     : in std_logic_vector(7 downto 0); -- Switches for input selection
        led    : out std_logic_vector(7 downto 0); -- LED output for result visualization
        uart_tx_sig: out std_logic  -- UART TX for full matrix output
    );
end MatrixMultiplier_FPGA;

architecture Behavioral of MatrixMultiplier_FPGA is
    signal start : std_logic := '0';
    signal done  : std_logic;
    signal A, B  : std_logic_vector((N*N*8)-1 downto 0);
    signal C     : std_logic_vector((N*N*16)-1 downto 0);
    signal clk_div : std_logic := '0';
    signal count : integer := 0;
    signal tx_data : std_logic_vector(7 downto 0);
    signal tx_start : std_logic := '0';
    signal tx_busy : std_logic;
    
    component MatrixMultiplierParallel
        generic (N : integer := 4);
        port (
            clk   : in std_logic;
            start : in std_logic;
            A, B  : in std_logic_vector((N*N*8)-1 downto 0);
            C     : out std_logic_vector((N*N*16)-1 downto 0);
            done  : out std_logic
        );
    end component;
    
    component UART_TX
        generic (
            CLOCK_FREQ : integer := 12000000;
            BAUD_RATE  : integer := 115200
        );
        port (
            clk     : in std_logic;
            start   : in std_logic;
            data_in : in std_logic_vector(7 downto 0);
            tx      : out std_logic;
            busy    : out std_logic
        );
    end component;
    
    signal send_index : integer := 0;
    signal send_active : std_logic := '0';
    
begin
    -- Clock Divider (Divide 12 MHz for easier timing)
    process(clk)
    begin
        if rising_edge(clk) then
            count <= count + 1;
            if count = 6000000 then -- Slow down (approx 2 Hz toggle)
                clk_div <= not clk_div;
                count <= 0;
            end if;
        end if;
    end process;
    
    -- Button triggers start signal
    process(clk_div)
    begin
        if rising_edge(clk_div) then
            start <= btn;
        end if;
    end process;
    
    -- Set input matrices (Hardcoded for now, can extend for user input)
    A <= "00000001000000100000001100000100" &
         "00000101000001100000011100001000" &
         "00001001000010100000101100001100" &
         "00001101000011100000111100010000";
    
    B <= "00000001000000100000001100000100" &
         "00000101000001100000011100001000" &
         "00001001000010100000101100001100" &
         "00001101000011100000111100010000";
    
    -- LED Output (Show part of result for debugging)
    led <= C(7 downto 0);
    
    -- Send Matrix C over UART
    process(clk_div)
    begin
        if rising_edge(clk_div) then
            if done = '1' and send_active = '0' then
                send_index <= 0;
                send_active <= '1';
            elsif send_active = '1' and tx_busy = '0' then
                if send_index < (N*N) then
                    tx_data <= C((send_index*16+7) downto send_index*16);
                    tx_start <= '1';
                    send_index <= send_index + 1;
                else
                    send_active <= '0';
                end if;
            end if;
        end if;
    end process;
    
    -- Instantiate MatrixMultiplierParallel
    uut: MatrixMultiplierParallel
        generic map (N => 4)
        port map (
            clk   => clk_div,
            start => start,
            A     => A,
            B     => B,
            C     => C,
            done  => done
        );
    
    -- Instantiate UART TX Module
    uart: UART_TX
        generic map (
            CLOCK_FREQ => 12000000,
            BAUD_RATE => 115200
        )
        port map (
            clk     => clk,
            start   => tx_start,
            data_in => tx_data,
            tx      => uart_tx_sig,
            busy    => tx_busy
        );
    
end Behavioral;
